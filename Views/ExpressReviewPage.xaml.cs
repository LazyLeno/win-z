using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WinZ.Models;
using WinZ.Services;
using WinZ.ViewModels;

namespace WinZ.Views;

public partial class ExpressReviewPage : Page
{
    private readonly List<SetupTask> _tasks;

    public ExpressReviewPage(List<SetupTask> tasks)
    {
        InitializeComponent();
        _tasks = tasks;
        BuildGroups();
        UpdateCount();
    }

    private void BuildGroups()
    {
        GroupsPanel.Children.Clear();

        // Create a 3-column grid
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); // Spacer
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); // Spacer
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        GroupsPanel.Children.Add(grid);

        var columns = new[] { new StackPanel(), new StackPanel(), new StackPanel() };
        Grid.SetColumn(columns[0], 0);
        Grid.SetColumn(columns[1], 2);
        Grid.SetColumn(columns[2], 4);
        grid.Children.Add(columns[0]);
        grid.Children.Add(columns[1]);
        grid.Children.Add(columns[2]);

        var groups = _tasks
            .GroupBy(t => t.SubCategory)
            .OrderBy(g => g.Key)
            .ToList();

        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            var colIdx = i % 3;
            var columnStack = columns[colIdx];

            var groupBorder = new Border { Margin = new Thickness(0, 0, 0, 15), VerticalAlignment = VerticalAlignment.Top };
            var groupStack  = new StackPanel();
            groupBorder.Child = groupStack;

            // Container for task items (the part that collapses)
            var itemsPanel = new StackPanel();
            foreach (var task in group)
            {
                itemsPanel.Children.Add(BuildTaskRow(task));
            }

            // Sub-Category header button with arrow
            var headerBtn = new Button
            {
                Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x25)),
                Margin = new Thickness(0, 0, 0, 5),
                Padding = new Thickness(10, 6, 10, 6),
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            var headerGrid = new Grid();
            headerGrid.Children.Add(new TextBlock
            {
                Text = group.Key,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            var arrow = new TextBlock
            {
                Text = "▲",
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerGrid.Children.Add(arrow);
            headerBtn.Content = headerGrid;

            // Toggle logic
            headerBtn.Click += (s, e) =>
            {
                bool isVisible = itemsPanel.Visibility == Visibility.Visible;
                itemsPanel.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
                arrow.Text = isVisible ? "▼" : "▲";
            };

            groupStack.Children.Add(headerBtn);
            groupStack.Children.Add(itemsPanel);
            columnStack.Children.Add(groupBorder);
        }
    }

    private UIElement BuildTaskRow(SetupTask task)
    {
        var border = new Border
        {
            Background      = new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21)),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding         = new Thickness(10, 8, 10, 8)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Checkbox
        var cb = new CheckBox
        {
            IsChecked      = task.IsSelected,
            VerticalAlignment = VerticalAlignment.Center,
            Margin         = new Thickness(0, 0, 10, 0)
        };
        cb.Checked   += (_, _) => { task.IsSelected = true;  UpdateCount(); };
        cb.Unchecked += (_, _) => { task.IsSelected = false; UpdateCount(); };
        Grid.SetColumn(cb, 0);

        // Icon (Image or Emoji)
        var iconGrid = new Grid { Width = 20, Height = 20, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        iconGrid.SetBinding(UIElement.VisibilityProperty, new Binding("HasIcon") { Source = task, Converter = (IValueConverter)Application.Current.Resources["BoolToVis"] });
        
        var img = new Image { Stretch = Stretch.Uniform };
        var binding = new Binding("IconImage") { Source = task };
        img.SetBinding(Image.SourceProperty, binding);
        
        var txt = new TextBlock { Text = task.Icon, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var visBinding = new Binding("IconImage") { Source = task, Converter = (IValueConverter)Application.Current.Resources["IconUrlToInvVis"] };
        txt.SetBinding(UIElement.VisibilityProperty, visBinding);
        
        iconGrid.Children.Add(img);
        iconGrid.Children.Add(txt);
        Grid.SetColumn(iconGrid, 1);

        // Name + Description
        var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        nameStack.Children.Add(new TextBlock
        {
            Text       = task.Name,
            FontWeight = FontWeights.SemiBold,
            FontSize   = 11,
            Foreground = Brushes.White
        });

        if (!string.IsNullOrEmpty(task.Description))
            nameStack.Children.Add(new TextBlock
            {
                Text       = task.Description,
                FontSize   = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                Margin     = new Thickness(0, 1, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
        Grid.SetColumn(nameStack, 2);

        grid.Children.Add(cb);
        grid.Children.Add(iconGrid);
        grid.Children.Add(nameStack);
        border.Child = grid;
        return border;
    }

    private void UpdateCount()
    {
        int n = _tasks.Count(t => t.IsSelected);
        CountLabel.Text = $"{n} of {_tasks.Count} items selected";
        RunBtn.IsEnabled = n > 0;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
        => NavigationService.GoBack();

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        var selected = _tasks.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0) return;

        var log = new LogService();
        var vm  = new RunningViewModel(selected, log);
        NavigationService.Navigate(new RunningPage(vm));
        await vm.RunAsync();
    }
}
