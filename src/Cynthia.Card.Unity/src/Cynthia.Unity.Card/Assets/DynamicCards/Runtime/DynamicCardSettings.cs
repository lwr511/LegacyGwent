using System;
using UnityEngine;

namespace Assets.Script.DynamicCards
{
    public static class DynamicCardSettings
    {
        private const string Preference = "DynamicCards.Enabled";
        public static event Action Changed;
        public static bool Enabled
        {
            get { return PlayerPrefs.GetInt(Preference, 0) == 1; }
            set
            {
                if (Enabled == value) return;
                PlayerPrefs.SetInt(Preference, value ? 1 : 0);
                PlayerPrefs.Save();
                if (Changed != null) Changed();
            }
        }
    }
}
