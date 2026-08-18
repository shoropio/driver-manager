using System;
using System.ComponentModel;
using System.Windows.Controls;
using DriverManager.App.ViewModels;

namespace DriverManager.App.Views;

public partial class DownloadsView : UserControl
{
    public DownloadsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            // Validation is now handled in ViewModel via property binding
        }
    }
}