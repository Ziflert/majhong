using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mahjong.UI
{
    /// <summary>
    /// Screen IDs of the game. The UIManager uses them as keys.
    /// </summary>
    public enum ScreenId
    {
        None = 0,
        MainMenu,
        Settings,
        Customization,
        Controls,
        About,
        GameHUD,
        PauseMenu,
        ResultScreen,
        Loading,
    }

    /// <summary>
    /// Global UI manager: controls screen transitions, layer ordering and pause.
    /// Lives on a dedicated Canvas hierarchy with 5 layers (see docs/ui-design.md).
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        private static UIManager _instance;

        public static UIManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("UIManager");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<UIManager>();
                }
                return _instance;
            }
        }

        /// <summary>Canvas root of all UI screens.</summary>
        public Canvas RootCanvas { get; private set; }

        /// <summary>Currently active screen.</summary>
        public ScreenId Current { get; private set; } = ScreenId.None;

        /// <summary>Screen that was active before opening a modal.</summary>
        public ScreenId Previous { get; private set; } = ScreenId.None;

        /// <summary>Whether a modal (pause, result, loading) is showing.</summary>
        public bool IsModalOpen => Current == ScreenId.PauseMenu
            || Current == ScreenId.ResultScreen
            || Current == ScreenId.Loading;

        /// <summary>Event fired when a screen becomes visible.</summary>
        public event Action<ScreenId> OnScreenOpened;

        /// <summary>Event fired when a screen is hidden.</summary>
        public event Action<ScreenId> OnScreenClosed;

        // Registered screens: ScreenId -> GameObject (the root panel).
        private readonly Dictionary<ScreenId, GameObject> _screens = new();

        // Layer sorting orders per screen.
        private static readonly Dictionary<ScreenId, int> SortingOrders = new()
        {
            { ScreenId.MainMenu, 900 },
            { ScreenId.Settings, 900 },
            { ScreenId.Customization, 901 },
            { ScreenId.Controls, 900 },
            { ScreenId.About, 900 },
            { ScreenId.GameHUD, 700 },
            { ScreenId.PauseMenu, 800 },
            { ScreenId.ResultScreen, 850 },
            { ScreenId.Loading, 950 },
        };

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        #region Screen registration

        /// <summary>Register a screen panel with the manager. Call once on startup.</summary>
        public void RegisterScreen(ScreenId id, GameObject panel)
        {
            if (panel == null) return;

            _screens[id] = panel;
            panel.SetActive(false);

            // Ensure the panel has a Canvas for its layer.
            var canvas = panel.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = panel.AddComponent<Canvas>();
                panel.AddComponent<GraphicRaycaster>();
            }

            if (SortingOrders.TryGetValue(id, out var order))
            {
                canvas.sortingOrder = order;
            }
        }

        /// <summary>Unregister a screen panel.</summary>
        public void UnregisterScreen(ScreenId id)
        {
            _screens.Remove(id);
        }

        #endregion

        #region Screen transitions

        /// <summary>Open a screen and close the current one. Clears the modal stack.</summary>
        public void OpenScreen(ScreenId id)
        {
            if (Current == id) return;

            // Close current (and any modal on top).
            if (Current != ScreenId.None)
            {
                CloseScreenInternal(Current);
                OnScreenClosed?.Invoke(Current);
            }

            Previous = Current;
            Current = id;

            if (id != ScreenId.None && _screens.TryGetValue(id, out var panel))
            {
                panel.SetActive(true);
                OnScreenOpened?.Invoke(id);
            }
        }

        /// <summary>Open a modal screen on top of the current one.</summary>
        public void OpenModal(ScreenId id)
        {
            if (_screens.TryGetValue(id, out var panel))
            {
                panel.SetActive(true);
                OnScreenOpened?.Invoke(id);
            }
        }

        /// <summary>Close a modal and return to the screen underneath.</summary>
        public void CloseModal(ScreenId id)
        {
            if (_screens.TryGetValue(id, out var panel))
            {
                panel.SetActive(false);
                OnScreenClosed?.Invoke(id);
            }
        }

        /// <summary>Close the current screen without opening a new one.</summary>
        public void CloseCurrent()
        {
            if (Current == ScreenId.None) return;
            CloseScreenInternal(Current);
            OnScreenClosed?.Invoke(Current);
            Current = ScreenId.None;
        }

        /// <summary>Return to the previous screen (used by back buttons).</summary>
        public void GoBack()
        {
            if (Previous == ScreenId.None) return;
            OpenScreen(Previous);
        }

        private void CloseScreenInternal(ScreenId id)
        {
            if (_screens.TryGetValue(id, out var panel))
            {
                panel.SetActive(false);
            }
        }

        #endregion

        #region Pause integration

        /// <summary>Whether the game world is paused by the UI.</summary>
        public bool IsWorldPaused { get; private set; }

        /// <summary>Pause or unpause the game world.</summary>
        public void SetWorldPaused(bool paused)
        {
            IsWorldPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
        }

        /// <summary>Toggle pause menu (Esc key handler).</summary>
        public void TogglePause()
        {
            if (Current == ScreenId.PauseMenu)
            {
                CloseModal(ScreenId.PauseMenu);
                SetWorldPaused(false);
            }
            else if (Current == ScreenId.GameHUD)
            {
                OpenModal(ScreenId.PauseMenu);
                SetWorldPaused(true);
            }
        }

        #endregion

        #region Input routing

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Esc from any screen: pause menu in game, back in menus.
                if (Current == ScreenId.GameHUD)
                {
                    TogglePause();
                }
                else if (IsModalOpen)
                {
                    if (Current == ScreenId.PauseMenu)
                    {
                        TogglePause();
                    }
                    else
                    {
                        CloseModal(Current);
                    }
                }
                else if (Current != ScreenId.MainMenu && Current != ScreenId.None)
                {
                    GoBack();
                }
            }
        }

        #endregion

        #region Screen lookup

        /// <summary>Get the registered panel for a screen, or null.</summary>
        public GameObject GetScreenPanel(ScreenId id)
        {
            return _screens.TryGetValue(id, out var panel) ? panel : null;
        }

        /// <summary>Check if a screen is currently visible.</summary>
        public bool IsScreenVisible(ScreenId id)
        {
            return _screens.TryGetValue(id, out var panel) && panel.activeSelf;
        }

        #endregion
    }
}
