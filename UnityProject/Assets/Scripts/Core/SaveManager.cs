using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using GoddessRequiem.Core;

namespace GoddessRequiem.Client.Core
{
    public class SaveManager : MonoBehaviour, IGameSystem
    {
        [Header("Configuration")]
        [SerializeField] private bool _encryptSaves = true;
        [SerializeField] private string _encryptionKey = "GoddessRequiem2024";
        [SerializeField] private int _maxBackupCount = 5;

        private string _saveDirectory;
        private readonly Dictionary<string, object> _memoryCache = new();

        public static SaveManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _saveDirectory = Path.Combine(Application.persistentDataPath, "saves");
            Directory.CreateDirectory(_saveDirectory);
        }

        public async UniTask InitializeAsync()
        {
            // Clean up old backups
            await CleanupOldBackupsAsync();
        }

        public void Shutdown()
        {
            _memoryCache.Clear();
        }

        public void OnGameStateChanged(GameState previous, GameState current) { }

        public async UniTask SaveAsync<T>(string key, T data)
        {
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);

            if (_encryptSaves)
            {
                json = Encrypt(json);
            }

            var filePath = GetFilePath(key);
            var backupPath = GetBackupPath(key);

            // Create backup
            if (File.Exists(filePath))
            {
                File.Copy(filePath, backupPath, true);
            }

            // Write new save
            await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);

            // Update memory cache
            _memoryCache[key] = data;
        }

        public async UniTask<T> LoadAsync<T>(string key)
        {
            // Check memory cache first
            if (_memoryCache.TryGetValue(key, out var cached) && cached is T typed)
            {
                return typed;
            }

            var filePath = GetFilePath(key);

            if (!File.Exists(filePath))
            {
                return default;
            }

            var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

            if (_encryptSaves)
            {
                json = Decrypt(json);
            }

            var data = JsonConvert.DeserializeObject<T>(json);

            // Update memory cache
            if (data != null)
            {
                _memoryCache[key] = data;
            }

            return data;
        }

        public async UniTask DeleteAsync(string key)
        {
            var filePath = GetFilePath(key);
            var backupPath = GetBackupPath(key);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            _memoryCache.Remove(key);
        }

        public bool Exists(string key)
        {
            return File.Exists(GetFilePath(key));
        }

        public List<string> GetAllSaveKeys()
        {
            var files = Directory.GetFiles(_saveDirectory, "*.save");
            var keys = new List<string>();

            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (!fileName.EndsWith(".backup"))
                {
                    keys.Add(fileName);
                }
            }

            return keys;
        }

        public async UniTask<SaveInfo> GetSaveInfoAsync(string key)
        {
            var filePath = GetFilePath(key);

            if (!File.Exists(filePath))
            {
                return null;
            }

            var info = new FileInfo(filePath);
            var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

            if (_encryptSaves)
            {
                json = Decrypt(json);
            }

            return new SaveInfo
            {
                Key = key,
                Size = info.Length,
                LastModified = info.LastWriteTimeUtc,
                Preview = GetSavePreview(json)
            };
        }

        private string GetFilePath(string key)
        {
            return Path.Combine(_saveDirectory, $"{SanitizeFileName(key)}.save");
        }

        private string GetBackupPath(string key)
        {
            return Path.Combine(_saveDirectory, $"{SanitizeFileName(key)}.backup.save");
        }

        private string SanitizeFileName(string fileName)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }

        private string Encrypt(string plainText)
        {
            var bytes = Encoding.UTF8.GetBytes(plainText);
            var keyBytes = Encoding.UTF8.GetBytes(_encryptionKey.PadRight(32).Substring(0, 32));

            using (var aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Key = keyBytes;
                aes.IV = new byte[16]; // In production, use a random IV stored with the data
                aes.Mode = System.Security.Cryptography.CipherMode.CBC;
                aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                {
                    var encrypted = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);
                    return Convert.ToBase64String(encrypted);
                }
            }
        }

        private string Decrypt(string cipherText)
        {
            var bytes = Convert.FromBase64String(cipherText);
            var keyBytes = Encoding.UTF8.GetBytes(_encryptionKey.PadRight(32).Substring(0, 32));

            using (var aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Key = keyBytes;
                aes.IV = new byte[16];
                aes.Mode = System.Security.Cryptography.CipherMode.CBC;
                aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                {
                    var decrypted = decryptor.TransformFinalBlock(bytes, 0, bytes.Length);
                    return Encoding.UTF8.GetString(decrypted);
                }
            }
        }

        private string GetSavePreview(string json)
        {
            try
            {
                var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (obj != null && obj.Count > 0)
                {
                    var firstKey = obj.Keys.GetEnumerator();
                    firstKey.MoveNext();
                    return $"{firstKey.Current}: {obj[firstKey.Current]?.ToString()?.Substring(0, Math.Min(50, obj[firstKey.Current]?.ToString().Length ?? 0))}";
                }
            }
            catch { }

            return json.Substring(0, Math.Min(100, json.Length));
        }

        private async UniTask CleanupOldBackupsAsync()
        {
            var backupFiles = Directory.GetFiles(_saveDirectory, "*.backup.save");

            foreach (var file in backupFiles)
            {
                var info = new FileInfo(file);
                if ((DateTime.UtcNow - info.LastWriteTimeUtc).TotalDays > 7)
                {
                    File.Delete(file);
                }
            }
        }
    }

    [System.Serializable]
    public class SaveInfo
    {
        public string Key;
        public long Size;
        public DateTime LastModified;
        public string Preview;
    }
}