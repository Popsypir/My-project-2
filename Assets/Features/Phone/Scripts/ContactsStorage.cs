using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamePhone
{
    /// <summary>
    /// Хранилище контактов, добавленных игроком - сохраняется между запусками
    /// через PlayerPrefs (JSON), как GameSettings. Список хранит ссылки на
    /// PhoneContact - удобно передавать сам объект в UI (см. ContactRowUI),
    /// а не искать его заново по имени/номеру.
    /// </summary>
    public static class ContactsStorage
    {
        private const string PrefsKey = "PhoneContacts";

        [Serializable]
        private class ContactListWrapper
        {
            public List<PhoneContact> Contacts = new();
        }

        private static List<PhoneContact> _contacts;

        public static event Action OnChanged;

        public static IReadOnlyList<PhoneContact> Contacts
        {
            get { EnsureLoaded(); return _contacts; }
        }

        public static void Add(string name, string number)
        {
            EnsureLoaded();
            _contacts.Add(new PhoneContact { Name = name, Number = number });
            Save();
        }

        public static void Remove(PhoneContact contact)
        {
            EnsureLoaded();
            _contacts.Remove(contact);
            Save();
        }

        private static void EnsureLoaded()
        {
            if (_contacts != null) return;

            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            var wrapper = string.IsNullOrEmpty(json)
                ? new ContactListWrapper()
                : JsonUtility.FromJson<ContactListWrapper>(json);

            _contacts = wrapper?.Contacts ?? new List<PhoneContact>();
        }

        private static void Save()
        {
            var wrapper = new ContactListWrapper { Contacts = _contacts };
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(wrapper));
            OnChanged?.Invoke();
        }
    }
}
