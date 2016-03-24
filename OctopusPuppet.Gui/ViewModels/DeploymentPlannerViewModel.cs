﻿using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using Caliburn.Micro;
using OctopusPuppet.DeploymentPlanner;
using OctopusPuppet.OctopusProvider;
using OctopusPuppet.Scheduler;
using QuickGraph;

namespace OctopusPuppet.Gui.ViewModels
{
    public class DeploymentPlannerViewModel : PropertyChangedBase
    {
        public DeploymentPlannerViewModel()
        {
            _branchDeploymentBranches = new List<string>();
            _selectedBranchDeploymentBranch = string.Empty;

            _branchDeploymentEnvironments = new List<string>();
            _selectedBranchDeploymentEnvironment = string.Empty;

            _redeploymentEnvironments = new List<string>();
            _selectedRedeploymentEnvironment = string.Empty;

            _environmentMirrorFromEnvironments = new List<string>();
            _selectedEnvironmentMirrorFromEnvironment = string.Empty;

            _environmentMirrorToEnvironments = new List<string>();
            _selectedEnvironmentMirrorToEnvironment = string.Empty;

            _layoutAlgorithmTypes = new List<string>(new []
            {
                "Tree",
                "Circular",
                "FR",
                "BoundedFR",
                "KK",
                "ISOM",
                "LinLog",
                "EfficientSugiyama",
                "Sugiyama",
                "CompoundFDP"
            });

            _selectedLayoutAlgorithmType = "EfficientSugiyama";

            OctopusApiKey = ConfigurationManager.AppSettings["OctopusApiKey"];
            OctopusUrl = ConfigurationManager.AppSettings["OctopusUrl"];
        }

        private bool _isLoadingData;
        public bool IsLoadingData
        {
            get { return _isLoadingData; }
            set
            {
                if (value == _isLoadingData) return;
                _isLoadingData = value;
                NotifyOfPropertyChange(() => IsLoadingData);
            }
        }

        private List<string> _branches;
        private List<string> Branches
        {
            get { return _branches; }
            set
            {
                if (value == _branches) return;
                _branches = value;
                BranchDeploymentBranches = value;
            }
        }

        private List<string> _environments;
        private List<string> Environments
        {
            get { return _environments; }
            set
            {
                if (value == _environments) return;
                _environments = value;

                BranchDeploymentEnvironments = value;
                RedeploymentEnvironments = value;
                EnvironmentMirrorFromEnvironments = value;
                EnvironmentMirrorToEnvironments = value;
            }
        }

        private List<DeploymentPlan> _deploymentPlans;
        private List<DeploymentPlan> DeploymentPlans
        {
            get { return _deploymentPlans; }
            set
            {
                if (value == _deploymentPlans) return;
                _deploymentPlans = value;
                NotifyOfPropertyChange(() => DeploymentPlans);
            }
        }

        private IBidirectionalGraph<ComponentVertex, ComponentEdge> _graph;
        public IBidirectionalGraph<ComponentVertex, ComponentEdge> Graph
        {
            get { return _graph; }
            private set
            {
                if (value == _graph) return;
                _graph = value;
                NotifyOfPropertyChange(() => Graph);
            }
        }

        private List<List<ComponentGroupVertex>> _products;
        public List<List<ComponentGroupVertex>> Products
        {
            get { return _products; }
            private set
            {
                if (value == _products) return;
                _products = value;
                NotifyOfPropertyChange(() => Products);
            }
        }

        private void GetBranchesAndEnvironments()
        {
            if (!string.IsNullOrEmpty(_octopusUrl) && !string.IsNullOrEmpty(_octopusApiKey))
            {
                IsLoadingData = true;
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        var deploymentPlanner = new OctopusDeploymentPlanner(_octopusUrl, _octopusApiKey);

                        Branches = deploymentPlanner.GetBranches();
                        Environments = deploymentPlanner.GetEnvironments();
                    }
                    catch
                    {
                        Branches = new List<string>();
                        Environments = new List<string>();                  
                    }
                }).ContinueWith(task => 
                {
                    IsLoadingData = false;
                });
            }
            else
            {
                Branches = new List<string>();
                Environments = new List<string>();
            }
            
        }

        private string _octopusApiKey;
        public string OctopusApiKey
        {
            get { return _octopusApiKey; }
            set
            {
                if (value == _octopusApiKey) return;
                _octopusApiKey = value;

                GetBranchesAndEnvironments();

                NotifyOfPropertyChange(() => OctopusApiKey);

                NotifyOfPropertyChange(() => CanBranchDeployment);
                NotifyOfPropertyChange(() => CanRedeployment);
                NotifyOfPropertyChange(() => CanEnvironmentMirror);
            }
        }

        private string _octopusUrl;
        public string OctopusUrl
        {
            get { return _octopusUrl; }
            set
            {
                if (value == _octopusUrl) return;
                _octopusUrl = value;

                GetBranchesAndEnvironments();

                NotifyOfPropertyChange(() => OctopusUrl);

                NotifyOfPropertyChange(() => CanBranchDeployment);
                NotifyOfPropertyChange(() => CanRedeployment);
                NotifyOfPropertyChange(() => CanEnvironmentMirror);
            }
        }

        private List<string> _branchDeploymentBranches;
        public List<string> BranchDeploymentBranches
        {
            get { return _branchDeploymentBranches; }
            private set
            {
                if (value == _branchDeploymentBranches) return;
                _branchDeploymentBranches = value;
                NotifyOfPropertyChange(() => BranchDeploymentBranches);
            }
        }

        private string _selectedBranchDeploymentBranch;
        public string SelectedBranchDeploymentBranch
        {
            get { return _selectedBranchDeploymentBranch; }
            set
            {
                if (value == _selectedBranchDeploymentBranch) return;
                _selectedBranchDeploymentBranch = value;
                NotifyOfPropertyChange(() => SelectedBranchDeploymentBranch);

                NotifyOfPropertyChange(() => CanBranchDeployment);
            }
        }

        private List<string> _branchDeploymentEnvironments;
        public List<string> BranchDeploymentEnvironments
        {
            get { return _branchDeploymentEnvironments; }
            private set
            {
                if (value == _branchDeploymentEnvironments) return;
                _branchDeploymentEnvironments = value;
                NotifyOfPropertyChange(() => BranchDeploymentEnvironments);
            }
        }

        private string _selectedBranchDeploymentEnvironment;
        public string SelectedBranchDeploymentEnvironment
        {
            get { return _selectedBranchDeploymentEnvironment; }
            set
            {
                if (value == _selectedBranchDeploymentEnvironment) return;
                _selectedBranchDeploymentEnvironment = value;
                NotifyOfPropertyChange(() => SelectedBranchDeploymentEnvironment);

                NotifyOfPropertyChange(() => CanBranchDeployment);
            }
        }

        private List<string> _redeploymentEnvironments;
        public List<string> RedeploymentEnvironments
        {
            get { return _redeploymentEnvironments; }
            private set
            {
                if (value == _redeploymentEnvironments) return;
                _redeploymentEnvironments = value;
                NotifyOfPropertyChange(() => RedeploymentEnvironments);
            }
        }

        private string _selectedRedeploymentEnvironment;
        public string SelectedRedeploymentEnvironment
        {
            get { return _selectedRedeploymentEnvironment; }
            set
            {
                if (value == _selectedRedeploymentEnvironment) return;
                _selectedRedeploymentEnvironment = value;
                NotifyOfPropertyChange(() => SelectedRedeploymentEnvironment);

                NotifyOfPropertyChange(() => CanRedeployment);
            }
        }

        private List<string> _environmentMirrorFromEnvironments;
        public List<string> EnvironmentMirrorFromEnvironments
        {
            get { return _environmentMirrorFromEnvironments; }
            private set
            {
                if (value == _environmentMirrorFromEnvironments) return;
                _environmentMirrorFromEnvironments = value;
                NotifyOfPropertyChange(() => EnvironmentMirrorFromEnvironments);
            }
        }

        private string _selectedEnvironmentMirrorFromEnvironment;
        public string SelectedEnvironmentMirrorFromEnvironment
        {
            get { return _selectedEnvironmentMirrorFromEnvironment; }
            set
            {
                if (value == _selectedEnvironmentMirrorFromEnvironment) return;
                _selectedEnvironmentMirrorFromEnvironment = value;
                NotifyOfPropertyChange(() => SelectedEnvironmentMirrorFromEnvironment);

                NotifyOfPropertyChange(() => CanEnvironmentMirror);
            }
        }

        private List<string> _environmentMirrorToEnvironments;
        public List<string> EnvironmentMirrorToEnvironments
        {
            get { return _environmentMirrorToEnvironments; }
            private set
            {
                if (value == _environmentMirrorToEnvironments) return;
                _environmentMirrorToEnvironments = value;
                NotifyOfPropertyChange(() => EnvironmentMirrorToEnvironments);
            }
        }

        private string _selectedEnvironmentMirrorToEnvironment;
        public string SelectedEnvironmentMirrorToEnvironment
        {
            get { return _selectedEnvironmentMirrorToEnvironment; }
            set
            {
                if (value == _selectedEnvironmentMirrorToEnvironment) return;
                _selectedEnvironmentMirrorToEnvironment = value;
                NotifyOfPropertyChange(() => SelectedEnvironmentMirrorToEnvironment);

                NotifyOfPropertyChange(() => CanEnvironmentMirror);
            }
        }

        public bool CanBranchDeployment
        {
            get
            {
                return !(string.IsNullOrEmpty(_octopusApiKey) 
                    || string.IsNullOrEmpty(_octopusApiKey) 
                    || string.IsNullOrEmpty(_selectedBranchDeploymentEnvironment) 
                    || string.IsNullOrEmpty(_selectedBranchDeploymentBranch));
            }
        }

        public void BranchDeployment()
        {
            if (!CanBranchDeployment) return;

            IsLoadingData = true;
            Task.Factory.StartNew(() =>
            {
                try
                {
                    var deploymentPlanner = new OctopusDeploymentPlanner(_octopusUrl, _octopusApiKey);
                    var branchDeploymentPlans = deploymentPlanner.GetBranchDeploymentPlans(_selectedBranchDeploymentEnvironment, _selectedBranchDeploymentBranch);
                    DeploymentPlans = branchDeploymentPlans.DeploymentPlans;

                    var deploymentScheduler = new DeploymentScheduler();

                    var componentGraph = deploymentScheduler.GetDeploymentComponentGraph(DeploymentPlans);
                    Graph = componentGraph.ToBidirectionalGraph();
                    Products = deploymentScheduler.GetDeploymentSchedule(componentGraph);
                }
                catch
                {
                    DeploymentPlans = new List<DeploymentPlan>();
                    Graph = null;
                    Products = new List<List<ComponentGroupVertex>>();
                }
            }).ContinueWith(task =>
            {
                IsLoadingData = false;
            });
        }

        public bool CanRedeployment
        {
            get
            {
                return !(string.IsNullOrEmpty(_octopusApiKey) 
                    || string.IsNullOrEmpty(_octopusApiKey) 
                    || string.IsNullOrEmpty(_selectedRedeploymentEnvironment));
            }
        }

        public void Redeployment()
        {
            if (!CanRedeployment) return;

            IsLoadingData = true;
            Task.Factory.StartNew(() =>
            {
                try
                {
                    var deploymentPlanner = new OctopusDeploymentPlanner(_octopusUrl, _octopusApiKey);
                    var redeployDeploymentPlans = deploymentPlanner.GetRedeployDeploymentPlans(_selectedRedeploymentEnvironment);
                    DeploymentPlans = redeployDeploymentPlans.DeploymentPlans;

                    var deploymentScheduler = new DeploymentScheduler();
                    var componentGraph = deploymentScheduler.GetDeploymentComponentGraph(DeploymentPlans);
                    Graph = componentGraph.ToBidirectionalGraph();
                    Products = deploymentScheduler.GetDeploymentSchedule(componentGraph);
                }
                catch
                {
                    DeploymentPlans = new List<DeploymentPlan>();
                    Graph = null;
                    Products = new List<List<ComponentGroupVertex>>();
                }
            }).ContinueWith(task =>
            {
                IsLoadingData = false;
            });
        }

        public bool CanEnvironmentMirror
        {
            get
            {
                return !(string.IsNullOrEmpty(_octopusApiKey) 
                    || string.IsNullOrEmpty(_octopusApiKey) 
                    || string.IsNullOrEmpty(_selectedEnvironmentMirrorFromEnvironment) 
                    || string.IsNullOrEmpty(_selectedEnvironmentMirrorToEnvironment));
            }
        }

        public void EnvironmentMirror()
        {
            if (!CanEnvironmentMirror) return;

            IsLoadingData = true;
            Task.Factory.StartNew(() =>
            {
                try
                {
                    var deploymentPlanner = new OctopusDeploymentPlanner(_octopusUrl, _octopusApiKey);
                    var environmentMirrorDeploymentPlans = deploymentPlanner.GetEnvironmentMirrorDeploymentPlans(_selectedEnvironmentMirrorFromEnvironment, _selectedEnvironmentMirrorToEnvironment);

                    DeploymentPlans = environmentMirrorDeploymentPlans.DeploymentPlans;

                    var deploymentScheduler = new DeploymentScheduler();
                    var componentGraph = deploymentScheduler.GetDeploymentComponentGraph(DeploymentPlans);
                    Graph = componentGraph.ToBidirectionalGraph();
                    Products = deploymentScheduler.GetDeploymentSchedule(componentGraph);
                }
                catch
                {
                    DeploymentPlans = new List<DeploymentPlan>();
                    Graph = null;
                    Products = new List<List<ComponentGroupVertex>>();
                }
            }).ContinueWith(task =>
            {
                IsLoadingData = false;
            });
        }

        private List<string> _layoutAlgorithmTypes;
        public List<string> LayoutAlgorithmTypes
        {
            get { return _layoutAlgorithmTypes; }
            set
            {
                if (value == _layoutAlgorithmTypes) return;
                _layoutAlgorithmTypes = value;
                NotifyOfPropertyChange(() => LayoutAlgorithmTypes);
            }
        }

        private string _selectedLayoutAlgorithmType;
        public string SelectedLayoutAlgorithmType
        {
            get { return _selectedLayoutAlgorithmType; }
            set
            {
                if (value == _selectedLayoutAlgorithmType) return;
                _selectedLayoutAlgorithmType = value;
                NotifyOfPropertyChange(() => SelectedLayoutAlgorithmType);
            }
        }
    }
}