using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibBox;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ControlBox
{
    public class InstallViewModel : ObservableObject
    {
        private ObservableCollection<string> _filenames;
        public ObservableCollection<string> Filenames
        {
            get => _filenames;
            set => SetProperty(ref _filenames, value);
        }

        private string _maindll;
        public string MainDll
        {
            get => _maindll;
            set => SetProperty(ref _maindll, value);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainDll) && !string.IsNullOrEmpty(MainDll))
            {
                ((RelayCommand)InstallCommand).NotifyCanExecuteChanged();
            }
            base.OnPropertyChanged(e);
        }

        public ICommand OpenCommand { get; }
        public ICommand InstallCommand { get; }

        private MainViewModel _main;

        public InstallViewModel(MainViewModel main)
        {
            _main = main;
            OpenCommand = new RelayCommand(Open);
            InstallCommand = new RelayCommand(Install, () => !string.IsNullOrWhiteSpace(MainDll));
        }

        private void Open()
        {
            var dlg = new OpenFileDialog()
            {
                Multiselect = true,
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = "dll",
                Filter = "Dll files (*.dll)|*.dll|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == false)
            {
                return;
            }

            Filenames = new ObservableCollection<string>(dlg.FileNames);
        }

        private async void Install()
        {
            var filenames = new List<string>();
            ReturnMessage result = null;
            foreach (var file in Filenames)
            {
                var name = Path.GetFileName(file);
                result = await _main.Transfer(new TransferMessage()
                {
                    Filename = name,
                    Type = "base64",
                    Content = Convert.ToBase64String(File.ReadAllBytes(file))
                });
                Debug.WriteLine($"{result.Type} {result.Result}");
                filenames.Add(name);
            }
            var maindll = Path.GetFileName(MainDll);
            result = await _main.Install(new InstallMessage()
            {
                MainDll = maindll,
                Filenames = filenames.ToArray()
            });

            MessageBox.Show($"{result.Type} {result.Result}");
        }
    }
}
