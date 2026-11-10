using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GoddessRequiem.Core.Application.Interfaces;
using GoddessRequiem.Core.Domain.Entities;
using GoddessRequiem.Core.Domain.Enums;
using GoddessRequiem.Core.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace GoddessRequiem.Core.Application.Services
{
    public class LeaderboardService : ILeaderboardService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<LeaderboardService> _logger;
        private readonly IRedisCache _redisCache;

        private const string LeaderboardPrefix = "leaderboard:";
        private const int DefaultPageSize = 50;

        public LeaderboardService(IUnitOfWork unitOfWork, ILogger<LeaderboardService> logger, IRedisCache redisCache)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _redisCache = redisCache;
        }

        public async Task UpdateScoreAsync(string leaderboardId, Guid userId, long score, Dictionary<string, object> metadata)
        {
            var key = LeaderboardPrefix + leaderboardId;
            var userKey = $"{key}:user:{userId}";

            // Update in Redis sorted set
            await _redisCache.SortedSetAddAsync(key, userId.ToString(), score);

            // Store metadata
            if (metadata.Any())
            {
                var metaKey = $"{key}:meta:{userId}";
                await _redisCache.HashSetAsync(metaKey, metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? ""));
                await _redisCache.KeyExpireAsync(metaKey, TimeSpan.FromDays(30));
            }

            // Store user's leaderboard membership
            await _redisCache.SetAddAsync($"{key}:users", userId.ToString());

            // Also persist to database periodically
            await PersistLeaderboardEntryAsync(leaderboardId, userId, score, metadata);
        }

        public async Task<IReadOnlyList<LeaderboardEntry>> GetTopAsync(string leaderboardId, int count)
        {
            var key = LeaderboardPrefix + leaderboardId;
            var topEntries = await _redisCache.SortedSetRangeByRankWithScoresAsync(key, 0, count - 1, Order.Descending);

            var entries = new List<LeaderboardEntry>();
            var rank = 1;

            foreach (var entry in topEntries)
            {
                if (Guid.TryParse(entry.Element, out var userId))
                {
                    var metadata = await _redisCache.HashGetAllAsync($"{key}:meta:{userId}");
                    entries.Add(new LeaderboardEntry
                    {
                        Rank = rank++,
                        UserId = userId,
                        Score = (long)entry.Score,
                        Metadata = metadata.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
                    });
                }
            }

            return entries;
        }

        public async Task<LeaderboardEntry> GetRankAsync(string leaderboardId, Guid userId)
        {
            var key = LeaderboardPrefix + leaderboardId;
            var rank = await _redisCache.SortedSetRankAsync(key, userId.ToString(), Order.Descending);
            var score = await _redisCache.SortedSetScoreAsync(key, userId.ToString());

            if (!rank.HasValue || !score.HasValue)
                return null;

            var metadata = await _redisCache.HashGetAllAsync($"{key}:meta:{userId}");

            return new LeaderboardEntry
            {
                Rank = rank.Value + 1,
                UserId = userId,
                Score = (long)score.Value,
                Metadata = metadata.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
            };
        }

        public async Task<IReadOnlyList<LeaderboardEntry>> GetAroundAsync(string leaderboardId, Guid userId, int range)
        {
            var key = LeaderboardPrefix + leaderboardId;
            var rank = await _redisCache.SortedSetRankAsync(key, userId.ToString(), Order.Descending);

            if (!rank.HasValue)
                return new List<LeaderboardEntry>();

            var start = Math.Max(0, rank.Value - range);
            var stop = rank.Value + range;

            var entries = await _redisCache.SortedSetRangeByRankWithScoresAsync(key, start, stop, Order.Descending);

            var result = new List<LeaderboardEntry>();
            var currentRank = start + 1;

            foreach (var entry in entries)
            {
                if (Guid.TryParse(entry.Element, out var entryUserId))
                {
                    var metadata = await _redisCache.HashGetAllAsync($"{key}:meta:{entryUserId}");
                    result.Add(new LeaderboardEntry
                    {
                        Rank = currentRank++,
                        UserId = entryUserId,
                        Score = (long)entry.Score,
                        Metadata = metadata.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
                    });
                }
            }

            return result;
        }

        public async Task ResetLeaderboardAsync(string leaderboardId)
        {
            var key = LeaderboardPrefix + leaderboardId;
            await _redisCache.KeyDeleteAsync(key);
            await _redisCache.KeyDeleteAsync($"{key}:users");

            // Delete metadata keys
            var metaKeys = await _redisCache.KeysAsync($"{key}:meta:*");
            if (metaKeys.Any())
            {
                await _redisCache.KeyDeleteAsync(metaKeys);
            }
        }

        public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(string leaderboardId, int page, int pageSize)
        {
            var key = LeaderboardPrefix + leaderboardId;
            var start = page * pageSize;
            var stop = start + pageSize - 1;

            var entries = await _redisCache.SortedSetRangeByRankWithScoresAsync(key, start, stop, Order.Descending);

            var result = new List<LeaderboardEntry>();
            var rank = start + 1;

            foreach (var entry in entries)
            {
                if (Guid.TryParse(entry.Element, out var userId))
                {
                    var metadata = await _redisCache.HashGetAllAsync($"{key}:meta:{userId}");
                    result.Add(new LeaderboardEntry
                    {
                        Rank = rank++,
                        UserId = userId,
                        Score = (long)entry.Score,
                        Metadata = metadata.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
                    });
                }
            }

            return result;
        }

        public async Task<long> GetTotalUsersAsync(string leaderboardId)
        {
            var key = LeaderboardPrefix + leaderboardId;
            return await _redisCache.SortedSetLengthAsync(key);
        }

        public async Task<LeaderboardStatsDto> GetLeaderboardStatsAsync(string leaderboardId)
        {
            var key = LeaderboardPrefix + leaderboardId;
            var totalUsers = await _redisCache.SortedSetLengthAsync(key);

            if (totalUsers == 0)
            {
                return new LeaderboardStatsDto { TotalUsers = 0 };
            }

            var topScore = await _redisCache.SortedSetRangeByRankWithScoresAsync(key, 0, 0, Order.Descending);
            var bottomScore = await _redisCache.SortedSetRangeByRankWithScoresAsync(key, 0, 0, Order.Ascending);

            return new LeaderboardStatsDto
            {
                TotalUsers = totalUsers,
                TopScore = topScore.Any() ? (long)topScore.First().Score : 0,
                LowestScore = bottomScore.Any() ? (long)bottomScore.First().Score : 0,
                AverageScore = 0 // Would calculate if needed
            };
        }

        private async Task PersistLeaderboardEntryAsync(string leaderboardId, Guid userId, long score, Dictionary<string, object> metadata)
        {
            // This would be called periodically via background job
            // For now, we just keep it in Redis
            await Task.CompletedTask;
        }

        public async Task ProcessSeasonEndAsync(string leaderboardId)
        {
            // Archive current leaderboard
            var archiveKey = $"{LeaderboardPrefix}{leaderboardId}:archive:{DateTime.UtcNow:yyyyMMdd}";
            var key = LeaderboardPrefix + leaderboardId;

            // Rename current to archive
            await _redisCache.KeyRenameAsync(key, archiveKey);
            await _redisCache.KeyRenameAsync($"{key}:users", $"{archiveKey}:users");

            // Log season end
            _logger.LogInformation("Leaderboard {LeaderboardId} season ended, archived to {ArchiveKey}", leaderboardId, archiveKey);
        }
    }
}