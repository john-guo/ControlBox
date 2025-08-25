using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibBox;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ControlBox
{
    public class TreeNode
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public bool IsFunction { get; set; }
        public FunctionMetaData MetaData { get; set; }
        public List<TreeNode> Children { get; set; }

        public TreeNode Parent { get; set; }
    }

    public class ParameterData
    {
        public string Name { get; set; }
        public object Value { get; set; }
    }

    public class CallData
    {
        public string Service { get; set; }
        public string Function { get; set; }
        public ObservableCollection<ParameterData> Data { get; set; }
    }

    public class MainViewModel : ObservableObject
    {
        private ObservableCollection<TreeNode> _services;

        public ObservableCollection<TreeNode> Services
        {
            get => _services;
            set => SetProperty(ref _services, value);
        }

        private CallData _callData;
        public CallData CallData
        {
            get => _callData;
            set => SetProperty(ref _callData, value);
        }

        public ICommand ListCommand { get; }
        public ICommand DisplayCommand { get; }
        public ICommand OpenInstallCommand { get; }
        public ICommand CallCommand { get; }        
        public ICommand UninstallCommand { get; }
        private Request _request;

        public MainViewModel()
        {
            _services = new ObservableCollection<TreeNode>();
            ListCommand = new AsyncRelayCommand(List);
            DisplayCommand = new RelayCommand<TreeNode>(Display);
            OpenInstallCommand = new RelayCommand(OpenInstall);
            CallCommand = new RelayCommand(Call);
            UninstallCommand = new RelayCommand(Uninstall);
            _request = new Request("localhost", 5001);
        }


        ~MainViewModel()
        {
            _request.Dispose();
        }

        private async void Uninstall()
        {
            if (string.IsNullOrWhiteSpace(CallData?.Service))
                return;
            var ret = await _request.Uninstall(new UninstallMessage() { ServiceName = CallData.Service });
            MessageBox.Show($"{ret.Type} {ret.Result}");
        }

        private async void Call()
        {
            if (string.IsNullOrWhiteSpace(CallData?.Function))
                return;
            var jo = new JObject();
            foreach (var item in CallData.Data)
            {
                jo.Add(item.Name, new JValue(item.Value));
            }

            var ret = await _request.Call(CallData.Service, CallData.Function, JsonConvert.SerializeObject(jo));
            MessageBox.Show($"{ret.Data}");
        }

        private void OpenInstall()
        {
            var installWindow = new Install();
            installWindow.DataContext = new InstallViewModel(this);
            installWindow.ShowDialog();
        }

        private void Display(TreeNode? node)
        {
            if (node == null || !node.IsFunction)
                return;

            CallData = new CallData()
            {
                Service = node.Parent.Name,
                Function = node.Name,
                Data = new ObservableCollection<ParameterData>(node.MetaData.Inputs.Select(m => new ParameterData() { Name = m.Name }))
            };
        }

        private async Task List()
        {
            var ret = await _request.List();
            _services.Clear();
            foreach (var service in ret)
            {
                var svc = new TreeNode { Name = service.Name, Children = [] };
                foreach (var func in service.Functions)
                {
                    var fnode = new TreeNode { Name = func.Name, Children = [], IsFunction = true, MetaData = func, Parent = svc };
                    var inode = new TreeNode { Name = "Parameters", Children = [], Parent = fnode };
                    fnode.Children.Add(inode);
                    foreach (var i in func.Inputs)
                    {
                        inode.Children.Add(new TreeNode { Name = i.Name, Value = string.IsNullOrEmpty(i.ElementType) ? i.Type : i.ElementType });
                    }
                    var pnode = new TreeNode { Name = "Properties", Children = [], Parent = fnode };
                    fnode.Children.Add(pnode);
                    foreach (var p in func.Properties)
                    {
                        pnode.Children.Add(new TreeNode { Name = p.Key, Value = $"{p.Value}" });
                    }
                    
                    svc.Children.Add(fnode);
                }
                
                _services.Add(svc);
            }
        }

        public async Task<ReturnMessage> Transfer(TransferMessage message)
        {
            return await _request.Transfer(message);
        }

        public async Task<ReturnMessage> Install(InstallMessage message)
        {
            return await _request.Install(message);
        }
    }
}
