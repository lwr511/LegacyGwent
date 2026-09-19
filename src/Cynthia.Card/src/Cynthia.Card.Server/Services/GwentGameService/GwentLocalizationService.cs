using System.Collections.Generic;
using System.IO;
using Cynthia.Card.Common.Models.Localization;
using Newtonsoft.Json;
using System;
using System.Security.Cryptography;
using System.Text;

namespace Cynthia.Card.Server.Services.GwentGameService
{
    public class GwentLocalizationService
    {
        private string _gameLocales;
        private readonly string _version;
        private readonly Dictionary<string, GameLocale> _locales = new Dictionary<string, GameLocale>();
        public GwentLocalizationService()
        {
            var config = new List<ConfigEntry>();
            var currentDirectory = Directory.GetCurrentDirectory();
            using (var stream = new StreamReader($"{currentDirectory}/Locales/config.json"))
            {
                var serializedConfig = stream.ReadToEnd();
                config = JsonConvert.DeserializeObject<List<ConfigEntry>>(serializedConfig);
            }

            var loadedLocales = new List<GameLocale>();
            foreach (var locale in config)
            {
                var filePath = $"{currentDirectory}/Locales/{locale.Filename}.json";
                var loadedLocale = JsonConvert.DeserializeObject<GameLocale>(File.ReadAllText(filePath));
                loadedLocales.Add(loadedLocale);
                _locales[locale.Filename] = loadedLocale;
            }

            _gameLocales = JsonConvert.SerializeObject(loadedLocales);
            using (var hash = SHA256.Create())
                _version = Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(_gameLocales)));
        }
        public string GetGameLocales()
        {
            return _gameLocales;
        }
        public string GetVersion() => _version;
        public string GetText(string language, string key)
        {
            var locale = _locales.TryGetValue(language ?? "en", out var selected) ? selected : _locales["en"];
            return locale.MenuLocales.TryGetValue(key, out var text) ? text : _locales["en"].MenuLocales[key];
        }
    }
}
