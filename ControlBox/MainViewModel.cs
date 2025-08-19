using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ControlBox
{
    public class TreeNode
    {
        public string Name { get; set;  }
        public string Value { get; set; }
        public bool IsFunction { get; set; }
        public FunctionMetaData MetaData { get; set; }
        public List<TreeNode> Children { get; set; }
    }

    public class MainViewModel : ObservableObject
    {
        private ObservableCollection<TreeNode> _services;

        public ObservableCollection<TreeNode> Services
        {
            get => _services;
            set => SetProperty(ref _services, value);
        }

        private FunctionMetaData _function;
        public FunctionMetaData Function
        {
            get => _function;
            set => SetProperty(ref _function, value);
        }

        public ICommand ListCommand { get; }
        public ICommand DisplayCommand { get; }
        private Request _request;

        public MainViewModel()
        {
            _services = new ObservableCollection<TreeNode>();
            ListCommand = new AsyncRelayCommand(List);
            DisplayCommand = new RelayCommand<TreeNode>(Display);
            _request = new Request("localhost", 5001);
        }

        ~MainViewModel()
        {
            _request.Dispose();
        }

        private void Display(TreeNode? node)
        {
            if (node == null || !node.IsFunction)
                return;
            Function = node.MetaData;
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
                    var fnode = new TreeNode { Name = func.Name, Children = [], IsFunction = true, MetaData = func };
                    var inode = new TreeNode { Name = "Parameters", Children = [] };
                    fnode.Children.Add(inode);
                    foreach (var i in func.Inputs)
                    {
                        inode.Children.Add(new TreeNode { Name = i.Name, Value = string.IsNullOrEmpty(i.ElementType) ? i.Type : i.ElementType });
                    }
                    var pnode = new TreeNode { Name = "Properties", Children = [] };
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
    }
}
