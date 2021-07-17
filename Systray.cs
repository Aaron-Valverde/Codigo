﻿using livelywpf.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Threading;

namespace livelywpf
{
    class Systray : IDisposable
    {
        private readonly System.Windows.Forms.NotifyIcon _notifyIcon = new System.Windows.Forms.NotifyIcon();
        private System.Windows.Forms.ToolStripMenuItem pauseTrayBtn;
        private System.Windows.Forms.ToolStripMenuItem customiseWallpaperBtn;
        public System.Windows.Forms.ToolStripMenuItem UpdateTrayBtn { get; private set; }
        private bool disposedValue;

        public Systray(bool visibility = true)
        {
            //NotifyIcon Fix: https://stackoverflow.com/questions/28833702/wpf-notifyicon-crash-on-first-run-the-root-visual-of-a-visualtarget-cannot-hav/29116917
            //Rarely I get this error "The root Visual of a VisualTarget cannot have a parent..", hard to pinpoint not knowing how to recreate the error.
            System.Windows.Controls.ToolTip tt = new System.Windows.Controls.ToolTip();
            tt.IsOpen = true;
            tt.IsOpen = false;

            _notifyIcon.DoubleClick += (s, args) => Program.ShowMainWindow();
            _notifyIcon.Icon = Properties.Icons.appicon;
            _notifyIcon.Text = Properties.Resources.TitleAppName;

            CreateContextMenu();
            _notifyIcon.Visible = visibility;
            Program.SettingsVM.TrayIconVisibilityChange += SettingsVM_TrayIconVisibilityChange;
            SetupDesktop.WallpaperChanged += SetupDesktop_WallpaperChanged;
            Playback.PlaybackStateChanged += Playback_PlaybackStateChanged;
        }

        private void CreateContextMenu()
        {
            _notifyIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip
            {
                ForeColor = Color.AliceBlue,
                Padding = new Padding(0),
                Margin = new Padding(0),
                //Font = new System.Drawing.Font("Segoe UI", 10F),
            };
            _notifyIcon.ContextMenuStrip.Opening += ContextMenuStrip_Opening;

            _notifyIcon.ContextMenuStrip.Renderer = new Helpers.CustomContextMenu.RendererDark();
            _notifyIcon.ContextMenuStrip.Items.Add(Properties.Resources.TextOpenLively, Properties.Icons.icons8_home_64).Click += (s, e) => Program.ShowMainWindow();

            _notifyIcon.ContextMenuStrip.Items.Add(Properties.Resources.TextCloseWallpapers, null).Click += (s, e) => SetupDesktop.TerminateAllWallpapers();

            pauseTrayBtn = new System.Windows.Forms.ToolStripMenuItem(Properties.Resources.TextPauseWallpapers, Properties.Icons.icons8_pause_52);
            pauseTrayBtn.Click += (s, e) => ToggleWallpaperPlaybackState();
            _notifyIcon.ContextMenuStrip.Items.Add(pauseTrayBtn);

            customiseWallpaperBtn = new System.Windows.Forms.ToolStripMenuItem(Properties.Resources.TextCustomiseWallpaper, null)
            {
                Enabled = false
            };
            customiseWallpaperBtn.Click += CustomiseWallpaper;
            _notifyIcon.ContextMenuStrip.Items.Add(customiseWallpaperBtn);

            if (!Program.IsMSIX)
            {
                _notifyIcon.ContextMenuStrip.Items.Add(new Helpers.CustomContextMenu.StripSeparatorCustom().stripSeparator);
                UpdateTrayBtn = new System.Windows.Forms.ToolStripMenuItem(Properties.Resources.TextUpdateChecking, null)
                {
                    Enabled = false
                };
                UpdateTrayBtn.Click += (s, e) => Program.ShowUpdateDialog();
                _notifyIcon.ContextMenuStrip.Items.Add(UpdateTrayBtn);
            }

            _notifyIcon.ContextMenuStrip.Items.Add(new Helpers.CustomContextMenu.StripSeparatorCustom().stripSeparator);
            _notifyIcon.ContextMenuStrip.Items.Add(Properties.Resources.TextSupport, Properties.Icons.icons8_heart_48).Click += (s, e) =>
                            Helpers.LinkHandler.OpenBrowser("https://ko-fi.com/rocksdanister");
            _notifyIcon.ContextMenuStrip.Items.Add(new Helpers.CustomContextMenu.StripSeparatorCustom().stripSeparator);
            _notifyIcon.ContextMenuStrip.Items.Add(Properties.Resources.TitleReportBug, Properties.Icons.icons8_bug_50).Click += (s, e) =>
                            Helpers.LinkHandler.OpenBrowser("https://github.com/rocksdanister/lively/wiki/Common-Problems");

            _notifyIcon.ContextMenuStrip.Items.Add(new Helpers.CustomContextMenu.StripSeparatorCustom().stripSeparator);
            _notifyIcon.ContextMenuStrip.Items.Add(Properties.Resources.TextExit, Properties.Icons.icons8_delete_52).Click += (s, e) => Program.ExitApplication();
        }

        /// <summary>
        /// Fix for when menu opens to the nearest screen instead of the screen in which cursor is located.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ContextMenuStrip menuStrip = (sender as ContextMenuStrip);
            if (ScreenHelper.IsMultiScreen())
            {
                //Finding screen in which cursor is present.
                var screen = ScreenHelper.GetScreenFromPoint(Cursor.Position);

                var mousePos = Cursor.Position;
                //Converting global cursor pos. to given screen pos.
                mousePos.X += -1 * screen.Bounds.X;
                mousePos.Y += -1 * screen.Bounds.Y;

                //guessing taskbar pos. based on cursor pos. on display.
                bool isLeft = mousePos.X < screen.Bounds.Width * .5;
                bool isTop = mousePos.Y < screen.Bounds.Height * .5;

                //menu popup pos. rule.
                if (isLeft && isTop)
                {
                    //not possible?
                    menuStrip.Show(Cursor.Position, ToolStripDropDownDirection.Default);
                }
                if (isLeft && !isTop)
                {
                    menuStrip.Show(Cursor.Position, ToolStripDropDownDirection.AboveRight);
                }
                else if (!isLeft && isTop)
                {
                    menuStrip.Show(Cursor.Position, ToolStripDropDownDirection.BelowLeft);
                }
                else if (!isLeft && !isTop)
                {
                    menuStrip.Show(Cursor.Position, ToolStripDropDownDirection.AboveLeft);
                }
            }
            else
            {
                menuStrip.Show(Cursor.Position, ToolStripDropDownDirection.AboveLeft);
            }
        }

        public void ShowBalloonNotification(int timeout, string title, string msg)
        {
            _notifyIcon.ShowBalloonTip(timeout, title, msg, ToolTipIcon.None);
        }

        private void CustomiseWallpaper(object sender, EventArgs e)
        {
            var items = SetupDesktop.Wallpapers.FindAll(x => x.GetWallpaperData().LivelyPropertyPath != null);
            if (items.Count == 0)
            {
                //not possible, menu should be disabled.
                //nothing..
            }
            else if (items.Count == 1)
            {
                //quick wallpaper customise tray widget.
                var settingsWidget = new Cef.LivelyPropertiesTrayWidget(items[0].GetWallpaperData());
                settingsWidget.Show();
            }
            else if (items.Count > 1)
            {
                switch (Program.SettingsVM.Settings.WallpaperArrangement)
                {
                    case WallpaperArrangement.per:
                        //multiple different wallpapers.. open control panel.
                        App.AppWindow?.ShowControlPanelDialog();
                        break;
                    case WallpaperArrangement.span:
                    case WallpaperArrangement.duplicate:
                        var settingsWidget = new Cef.LivelyPropertiesTrayWidget(items[0].GetWallpaperData());
                        settingsWidget.Show();
                        break;
                }
            }
        }

        private void Playback_PlaybackStateChanged(object sender, PlaybackState e)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                pauseTrayBtn.Checked = (e == PlaybackState.paused);
                _notifyIcon.Icon = (e == PlaybackState.paused) ? Properties.Icons.appicon_gray : Properties.Icons.appicon;
            }));
        }

        private void ToggleWallpaperPlaybackState()
        {
            Playback.WallpaperPlaybackState = (Playback.WallpaperPlaybackState == PlaybackState.play) ? Playback.WallpaperPlaybackState = PlaybackState.paused : PlaybackState.play;
        }

        private void SetupDesktop_WallpaperChanged(object sender, EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                customiseWallpaperBtn.Enabled = SetupDesktop.Wallpapers.FindAll(x => x.GetWallpaperData().LivelyPropertyPath != null).Count != 0;
            }));
        }

        private void SettingsVM_TrayIconVisibilityChange(object sender, bool visibility)
        {
            TrayIconVisibility(visibility);
        }

        private void TrayIconVisibility(bool visibility)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = visibility;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Program.SettingsVM.TrayIconVisibilityChange -= SettingsVM_TrayIconVisibilityChange;
                    Playback.PlaybackStateChanged -= Playback_PlaybackStateChanged;
                    _notifyIcon.Visible = false;
                    _notifyIcon?.Icon.Dispose();
                    _notifyIcon?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~Systray()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}