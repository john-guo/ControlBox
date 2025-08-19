using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ControlBox
{
    public class PropertyEditor : ContentControl
    {
        public object DataContextObject
        {
            get => GetValue(DataContextObjectProperty);
            set
            {
                SetValue(DataContextObjectProperty, value);
            }
        }

        public static readonly DependencyProperty DataContextObjectProperty =
            DependencyProperty.Register(nameof(DataContextObject), typeof(object), typeof(PropertyEditor),
                new PropertyMetadata(OnPropertyChanged));

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var pe = d as PropertyEditor;
            if (pe == null)
                return;
            pe.Content = pe.GeneratePropertiesUI(pe.DataContextObject);
        }


        private FrameworkElement GeneratePropertiesUI(object context)
        {
            if (context == null) return null;

            var properties = context.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite);


            var grid = new Grid
            {
                Margin = new Thickness(10)
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });


            foreach (var property in properties)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            int rowIndex = 0;
            foreach (var property in properties)
            {
                if (property.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
                {
                    var array = property.GetValue(context) as IEnumerable;
                    var expander = new Expander() { Header = property.Name };
                    var panel = new StackPanel();
                    foreach (var item in array)
                    {
                        panel.Children.Add(GeneratePropertiesUI(item));
                    }
                    expander.Content = panel;
                    Grid.SetRow(expander, rowIndex++);
                    Grid.SetColumn(expander, 0);
                    Grid.SetColumnSpan(expander, 2);
                    grid.Children.Add(expander);
                    continue;
                }

                var label = new TextBlock
                {
                    Text = property.Name,
                    Margin = new Thickness(5)
                };
                Grid.SetRow(label, rowIndex);
                Grid.SetColumn(label, 0);
                grid.Children.Add(label);

                var editor = new TextBox
                {
                    Margin = new Thickness(5)
                };
                editor.SetBinding(TextBox.TextProperty, new Binding(property.Name)
                {
                    Source = context,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });
                Grid.SetRow(editor, rowIndex);
                Grid.SetColumn(editor, 1);
                grid.Children.Add(editor);

                rowIndex++;
            }

            return grid;
        }
    }
}
