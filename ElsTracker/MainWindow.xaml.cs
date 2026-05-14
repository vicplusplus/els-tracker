using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ElsTracker.Services;
using ElsTracker.ViewModels;

namespace ElsTracker;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _timer;

    // DWM attribute for dark title bar (Win10 2004+ / Win11).
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int useDark = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        }
        catch { /* older Windows: silently fall back to light chrome */ }
    }

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        _vm.RowAdded += FocusIgnFor;

        TheGrid.AddHandler(DataGridColumnHeader.ClickEvent,
            new RoutedEventHandler(OnColumnHeaderClick));

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += (_, _) => _vm.Tick();
        _timer.Start();
    }

    // ---- raid column header click ----

    private static readonly HashSet<string> RaidHeaders =
        new(StringComparer.Ordinal) { "Doom", "Serp", "Abyss", "Challenge" };

    private void OnColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not DataGridColumnHeader hdr) return;
        if (hdr.Content is not string raid) return;
        if (!RaidHeaders.Contains(raid)) return;
        _vm.CopyRaidUncleared(raid);
    }

    // ---- focus newly added row's IGN field ----

    private void FocusIgnFor(CharacterRow row)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            TheGrid.UpdateLayout();
            TheGrid.ScrollIntoView(row);
            TheGrid.UpdateLayout();

            var rowContainer = TheGrid.ItemContainerGenerator.ContainerFromItem(row) as DataGridRow;
            if (rowContainer == null) return;

            // Find the IGN column index dynamically.
            var ignCol = TheGrid.Columns.FirstOrDefault(c => c is DataGridTemplateColumn t && t.Header is string s && s == "IGN");
            if (ignCol == null) return;

            var presenter = FindVisualChild<DataGridCellsPresenter>(rowContainer);
            if (presenter == null) return;
            var cell = presenter.ItemContainerGenerator.ContainerFromIndex(ignCol.DisplayIndex) as DataGridCell;
            if (cell == null) return;

            var tb = FindVisualChild<TextBox>(cell);
            if (tb != null)
            {
                tb.Focus();
                Keyboard.Focus(tb);
                tb.SelectAll();
            }
        }), DispatcherPriority.Background);
    }

    // ---- click-off-to-defocus for IGN textbox ----

    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject src) return;

        // Don't disrupt focus if click landed inside an interactive control.
        if (FindAncestor<TextBox>(src) != null) return;
        if (FindAncestor<ButtonBase>(src) != null) return;
        if (FindAncestor<Popup>(src) != null) return;

        // Click was on background / row / cell chrome. Drop focus from the IGN textbox.
        var focused = Keyboard.FocusedElement as DependencyObject;
        if (focused is TextBox)
        {
            Keyboard.ClearFocus();
            FocusManager.SetFocusedElement(this, this);
        }
    }

    // ---- class picker handlers ----

    private void ClassToggle_Checked(object sender, RoutedEventArgs e)
    {
        var toggle = (ToggleButton)sender;
        var grid = (Grid)toggle.Parent;
        var search = (TextBox)((StackPanel)grid.FindName("PopupRoot")!).FindName("SearchBox")!;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            search.Text = "";
            search.Focus();
            Keyboard.Focus(search);
        }), DispatcherPriority.Background);
    }

    private void ClassList_Loaded(object sender, RoutedEventArgs e)
    {
        var lb = (ListBox)sender;
        if (lb.ItemsSource is ListCollectionView) return;
        if (lb.ItemsSource is not System.Collections.IEnumerable src) return;
        var items = src.Cast<ClassItem>().ToList();
        lb.ItemsSource = new ListCollectionView(items);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var tb = (TextBox)sender;
        var sp = (StackPanel)tb.Parent;
        if (sp.FindName("ClassList") is not ListBox lb) return;
        if (lb.ItemsSource is not ListCollectionView view) return;

        var text = (tb.Text ?? "").Trim();
        view.Filter = text.Length == 0
            ? null
            : (Predicate<object>)(item =>
                item is ClassItem ci &&
                ci.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        var tb = (TextBox)sender;
        var sp = (StackPanel)tb.Parent;
        if (sp.FindName("ClassList") is not ListBox lb) return;

        if (e.Key == Key.Enter)
        {
            object? first = null;
            if (lb.ItemsSource is ListCollectionView view)
                foreach (var item in view) { first = item; break; }
            if (first is ClassItem ci)
            {
                CommitClassSelection(lb, ci);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape)
        {
            ClosePopupContaining(lb);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            lb.Focus();
            if (lb.Items.Count > 0 && lb.SelectedIndex < 0) lb.SelectedIndex = 0;
            e.Handled = true;
        }
    }

    private void ClassList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var lb = (ListBox)sender;
        if (e.OriginalSource is DependencyObject dep)
        {
            var container = ItemsControl.ContainerFromElement(lb, dep) as ListBoxItem;
            if (container?.DataContext is ClassItem ci)
            {
                CommitClassSelection(lb, ci);
                e.Handled = true;
            }
        }
    }

    private void CommitClassSelection(ListBox lb, ClassItem ci)
    {
        if (lb.DataContext is CharacterRow row)
            row.ClassName = ci.Name;
        ClosePopupContaining(lb);
    }

    private void ClosePopupContaining(DependencyObject d)
    {
        DependencyObject? cur = d;
        while (cur is not null)
        {
            if (cur is Popup popup) { popup.IsOpen = false; return; }
            cur = LogicalTreeHelper.GetParent(cur) ?? VisualTreeHelper.GetParent(cur);
        }
        var grid = FindAncestor<Grid>(d);
        if (grid?.FindName("ClassToggle") is ToggleButton tg)
            tg.IsChecked = false;
    }

    private static T? FindAncestor<T>(DependencyObject d) where T : DependencyObject
    {
        DependencyObject? cur = d;
        while (cur != null)
        {
            if (cur is T t) return t;
            cur = LogicalTreeHelper.GetParent(cur) ?? VisualTreeHelper.GetParent(cur);
        }
        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var sub = FindVisualChild<T>(child);
            if (sub != null) return sub;
        }
        return null;
    }
}
