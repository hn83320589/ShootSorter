using System.Drawing;
using System.Windows;
using ShootSorter.Core.Watching;
using Forms = System.Windows.Forms;

namespace ShootSorter.App.Services;

/// <summary>
/// 系統匣常駐：最小化時隱藏視窗只留圖示，雙擊圖示或選單「開啟」還原；每批處理完顯示氣泡摘要。
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const int BalloonTimeoutMs = 5000;

    private readonly Window _window;
    private readonly Forms.NotifyIcon _notifyIcon;

    public TrayIcon(Window window, Icon icon)
    {
        _window = window;

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("開啟主視窗", null, (_, _) => Restore());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("結束 ShootSorter", null, (_, _) => _window.Close());

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = icon,
            Text = "ShootSorter 攝影棚素材自動歸檔",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => Restore();
        _notifyIcon.BalloonTipClicked += (_, _) => Restore();

        _window.StateChanged += (_, _) =>
        {
            if (_window.WindowState == WindowState.Minimized)
            {
                _window.Hide();
            }
        };
    }

    public void ShowBatchSummary(BatchSummary summary)
    {
        _notifyIcon.ShowBalloonTip(
            BalloonTimeoutMs,
            "ShootSorter 本批處理完成",
            summary.Describe(),
            summary.Failed > 0 ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info);
    }

    public void Restore()
    {
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public void Dispose()
    {
        // 不先隱藏的話，程式結束後系統匣會殘留圖示直到滑鼠移過去。
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
