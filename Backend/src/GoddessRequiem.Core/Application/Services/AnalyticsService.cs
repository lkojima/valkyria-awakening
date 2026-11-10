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
    public class AnalyticsService : IAnalyticsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AnalyticsService> _logger;
        private readonly IAnalyticsProvider _analyticsProvider;

        public AnalyticsService(IUnitOfWork unitOfWork, ILogger<AnalyticsService> logger, IAnalyticsProvider analyticsProvider)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _analyticsProvider = analyticsProvider;
        }

        public async Task TrackEventAsync(string eventName, Dictionary<string, object> parameters)
        {
            // Send to analytics provider (Firebase, custom, etc.)
            await _analyticsProvider.TrackEventAsync(eventName, parameters);

            // Also store locally for debugging/analysis
            var analyticsEvent = new AnalyticsEvent
            {
                EventName = eventName,
                Parameters = parameters,
                Timestamp = DateTime.UtcNow
            };

            await _unitOfWork.AnalyticsEvents.AddAsync(analyticsEvent);

            // Batch save periodically
            if (DateTime.UtcNow.Second % 10 == 0)
            {
                await _unitOfWork.SaveChangesAsync();
            }
        }

        public async Task TrackPurchaseAsync(Guid userId, string productId, decimal price, string currency)
        {
            var parameters = new Dictionary<string, object>
            {
                { "user_id", userId.ToString() },
                { "product_id", productId },
                { "price", price },
                { "currency", currency },
                { "timestamp", DateTime.UtcNow }
            };

            await _analyticsProvider.TrackPurchaseAsync(userId, productId, price, currency);
            await TrackEventAsync("purchase", parameters);

            // Update user lifetime value
            await UpdateUserLTVAsync(userId, price);
        }

        public async Task TrackProgressionAsync(Guid userId, string milestone, Dictionary<string, object> data)
        {
            var parameters = new Dictionary<string, object>(data)
            {
                { "user_id", userId.ToString() },
                { "milestone", milestone },
                { "timestamp", DateTime.UtcNow }
            };

            await _analyticsProvider.TrackProgressionAsync(userId, milestone, data);
            await TrackEventAsync("progression", parameters);
        }

        public async Task TrackRetentionAsync(Guid userId, int day)
        {
            var parameters = new Dictionary<string, object>
            {
                { "user_id", userId.ToString() },
                { "retention_day", day },
                { "timestamp", DateTime.UtcNow }
            };

            await _analyticsProvider.TrackRetentionAsync(userId, day);
            await TrackEventAsync("retention", parameters);
        }

        public async Task TrackErrorAsync(string error, Dictionary<string, object> context)
        {
            var parameters = new Dictionary<string, object>(context)
            {
                { "error", error },
                { "timestamp", DateTime.UtcNow }
            };

            await _analyticsProvider.TrackErrorAsync(error, context);
            await TrackEventAsync("error", parameters);

            // Log for monitoring
            _logger.LogError("Analytics Error: {Error} Context: {@Context}", error, context);
        }

        public async Task TrackSessionAsync(Guid userId, TimeSpan duration, SessionType type)
        {
            var parameters = new Dictionary<string, object>
            {
                { "user_id", userId.ToString() },
                { "session_duration_seconds", duration.TotalSeconds },
                { "session_type", type.ToString() },
                { "timestamp", DateTime.UtcNow }
            };

            await TrackEventAsync("session", parameters);
        }

        public async Task TrackFunnelAsync(string funnelName, string stepName, int stepOrder, bool completed)
        {
            var parameters = new Dictionary<string, object>
            {
                { "funnel_name", funnelName },
                { "step_name", stepName },
                { "step_order", stepOrder },
                { "completed", completed },
                { "timestamp", DateTime.UtcNow }
            };

            await TrackEventAsync("funnel", parameters);
        }

        public async Task TrackResourceFlowAsync(Guid userId, CurrencyType currency, long amount, ResourceFlowType flowType, string source)
        {
            var parameters = new Dictionary<string, object>
            {
                { "user_id", userId.ToString() },
                { "currency", currency.ToString() },
                { "amount", amount },
                { "flow_type", flowType.ToString() },
                { "source", source },
                { "timestamp", DateTime.UtcNow }
            };

            await TrackEventAsync("resource_flow", parameters);
        }

        public async Task TrackBattleAsync(Guid userId, GameMode mode, BattleResult result, int turns, TimeSpan duration, List<Guid> characterIds)
        {
            var parameters = new Dictionary<string, object>
            {
                { "user_id", userId.ToString() },
                { "mode", mode.ToString() },
                { "result", result.ToString() },
                { "turns", turns },
                { "duration_seconds", duration.TotalSeconds },
                { "character_count", characterIds.Count },
                { "characters", string.Join(",", characterIds) },
                { "timestamp", DateTime.UtcNow }
            };

            await TrackEventAsync("battle", parameters);
        }

        public async Task TrackGachaAsync(Guid userId, Guid bannerId, int pullCount, List<Rarity> rarities, List<Guid> characterIds)
        {
            var parameters = new Dictionary<string, object>
            {
                { "user_id", userId.ToString() },
                { "banner_id", bannerId.ToString() },
                { "pull_count", pullCount },
                { "rarities", string.Join(",", rarities) },
                { "character_ids", string.Join(",", characterIds) },
                { "ssr_count", rarities.Count(r => r >= Rarity.SSR) },
                { "ur_count", rarities.Count(r => r == Rarity.UR) },
                { "timestamp", DateTime.UtcNow }
            };

            await TrackEventAsync("gacha", parameters);
        }

        private async Task UpdateUserLTVAsync(Guid userId, decimal amount)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user != null)
            {
                user.LifetimeValue += amount;
                await _unitOfWork.Users.UpdateAsync(user);
            }
        }
    }
}