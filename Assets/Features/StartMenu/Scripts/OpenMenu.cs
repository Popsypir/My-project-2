using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Runtime.CompilerServices;
using System;
using Unity.VisualScripting;

public class OpenMenu : MonoBehaviour
{
    // ---------------------- Переменные
    private int isIndex = 0;

    // ---------------------- SerializeField
    [Header("Панели")]
    [SerializeField] private CanvasGroup creditsPanel;
    [SerializeField] private CanvasGroup settingsPanel;
    [SerializeField] private CanvasGroup ExitPanel;
    [SerializeField] private CanvasGroup MPGamesPanel;
    [SerializeField] private CanvasGroup LoadGamesPanel;

    [Header("Тексты кнопок")]
    [SerializeField] private TextMeshProUGUI authorsTextButton;
    [SerializeField] private TextMeshProUGUI SettingsTextButton;
    [SerializeField] private TextMeshProUGUI ExitTextButton;
    [SerializeField] private TextMeshProUGUI MPGamesTextButton;
    [SerializeField] private TextMeshProUGUI LoadGamesTextButton;

    [Header("Кнопки")]
    [SerializeField] private GameObject SettingsButton;
    [SerializeField] private GameObject CreditsButton;
    [SerializeField] private GameObject ExitButton;
    [SerializeField] private GameObject MPGamesButton;
    [SerializeField] private GameObject LoadGamesButton;

    public void OpenMenus(int index)
    {
        switch (index)
        {
            case 1:
                if (isIndex == 1) { Close(1); isIndex = 0; }       // Если настройки уже открыты - закрываем
                else { CloseOtherMenus(1); Open(1); }     // Производим закрытие остальных меню и открытие настроек
                break;
            case 2:
                if (isIndex == 2) { Close(2); isIndex = 0; }     // аналогично с настройками
                else { CloseOtherMenus(2); Open(2); }         
                break;
            case 3:
                if (isIndex == 3) { Close(3); isIndex = 0; }     // аналогично с настройками
                else { CloseOtherMenus(3); Open(3); }
                break;
            case 4:
                if (isIndex == 4) { Close(4); isIndex = 0; }     // аналогично с настройками
                else { CloseOtherMenus(4); Open(4); }
                break;
            case 5:
                if (isIndex == 5) { Close(5); isIndex = 0; }     // аналогично с настройками
                else { CloseOtherMenus(5); Open(5); }
                break;
        }
    }

// ----------------- Открытия, закрытия меню
    public void Open(int index)
    {
        isIndex = index;
        CanvasGroup Panel = null;
        TextMeshProUGUI Text = null;
        
        switch (index) {
            case 1: Panel = settingsPanel; Text = SettingsTextButton; break;
            case 2: Panel = creditsPanel; Text = authorsTextButton; break;
            case 3: Panel = ExitPanel; Text = ExitTextButton; break;
            case 4: Panel = MPGamesPanel; Text = MPGamesTextButton; break;
            case 5: Panel = LoadGamesPanel; Text = LoadGamesTextButton; break;
        }

        Panel.interactable = true; Panel.alpha = 1; Panel.blocksRaycasts = true;
        Text.text = "НАЗАД";
    }

    public void Close(int index)
    {
        CanvasGroup Panel = null;
        TextMeshProUGUI Text = null;
        string str_text = null;

        switch (index)
        {
            case 1: Panel = settingsPanel; Text = SettingsTextButton; str_text = "НАСТРОЙКИ"; break;
            case 2: Panel = creditsPanel; Text = authorsTextButton; str_text = "ТИТРЫ"; break;
            case 3: Panel = ExitPanel; Text = ExitTextButton; str_text = "ВЫЙТИ ИЗ ИГРЫ"; break;
            case 4: Panel = MPGamesPanel; Text = MPGamesTextButton; str_text = "СЕТЕВАЯ ИГРА"; break;
            case 5: Panel = LoadGamesPanel; Text = LoadGamesTextButton; str_text = "ЗАГРУЗИТЬ ИГРУ"; break;
        }

        Panel.interactable = false; Panel.alpha = 0; Panel.blocksRaycasts = false;
        Text.text = str_text;
        EventSystem.current.SetSelectedGameObject(null);

        if (isIndex == index)
        {
            isIndex = 0;
        }
    }

    public int getIndex()
    {
        return isIndex;
    }

    private void CloseOtherMenus(int index)
    {
        switch (index)
        {
            case 1: Array.ForEach(new[] { 2, 3, 4, 5 }, Close); break;   // Закрываем все остальные меню
            case 2: Array.ForEach(new[] { 1, 3, 4, 5 }, Close); break;
            case 3: Array.ForEach(new[] { 1, 2, 4, 5 }, Close); break;
            case 4: Array.ForEach(new[] { 1, 2, 3, 5 }, Close); break;
            case 5: Array.ForEach(new[] { 1, 2, 3, 4 }, Close); break;
        }
    }

    private void Update()
    {
        if (isIndex != 0) // Проверка на открыто ли хотя бы одно меню
        {
            switch (isIndex)
            {
                case 1:
                    if (EventSystem.current.currentSelectedGameObject != SettingsButton) { EventSystem.current.SetSelectedGameObject(SettingsButton); }
                    break;
                case 2:
                    if (EventSystem.current.currentSelectedGameObject != CreditsButton) { EventSystem.current.SetSelectedGameObject(CreditsButton); }
                    break;
                case 3:
                    if (EventSystem.current.currentSelectedGameObject != ExitButton) { EventSystem.current.SetSelectedGameObject(ExitButton); }
                    break;
                case 4:
                    if (EventSystem.current.currentSelectedGameObject != MPGamesButton) { EventSystem.current.SetSelectedGameObject(MPGamesButton); }
                    break;
                case 5:
                    if (EventSystem.current.currentSelectedGameObject != LoadGamesButton) { EventSystem.current.SetSelectedGameObject(LoadGamesButton); }
                    break;
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape) && isIndex != 0) // Если хотя бы одно меню открыто и нажата кнопка ESC
        {
            if (isIndex == 1) { Close(1); isIndex = 0; }    // Проверка на конкретное меню и закрытие
            if (isIndex == 2) { Close(2); isIndex = 0; }
            if (isIndex == 3) { Close(3); isIndex = 0; }
            if (isIndex == 4) { Close(4); isIndex = 0; }
            if (isIndex == 5) { Close(5); isIndex = 0; }
        }
    }
}
