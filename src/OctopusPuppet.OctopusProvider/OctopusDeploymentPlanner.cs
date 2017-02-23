﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using Newtonsoft.Json;
using Octopus.Client;
using Octopus.Client.Model;
using OctopusPuppet.DeploymentPlanner;

namespace OctopusPuppet.OctopusProvider
{
    public class OctopusDeploymentPlanner : IDeploymentPlanner
    {
        private const string ComponentDependancies = "ComponentDependencies";
        private readonly IOctopusRepository _repository;

        public OctopusDeploymentPlanner(string url, string apiKey) : this(new OctopusRepository(new OctopusServerEndpoint(url, apiKey))) {}

        public OctopusDeploymentPlanner(IOctopusRepository repository)
        {
            _repository = repository;
        }

        private List<ReleaseResource> GetReleaseResources(string projectId)
        {
            var project = _repository.Projects.Get(projectId);

            var releases = new List<ReleaseResource>();
            var skip = 0;
            var shouldPage = false;
            do
            {
                var releasePages = _repository.Projects.GetReleases(project, skip);
                releases.AddRange(releasePages.Items);
                skip += releasePages.ItemsPerPage;
                shouldPage = releasePages.TotalResults > skip;
            } while (shouldPage);

            return releases;
        }

        private DashboardItemResource GetClosestMatchingDashboardItemResource(DashboardResource dashboard, string environmentId, string projectId, string branch)
        {
            var dashboardItemResources = dashboard.Items
                .Where(x => x.ProjectId == projectId);

            if (dashboardItemResources.Any(x => x.EnvironmentId == environmentId))
            {
                dashboardItemResources = dashboardItemResources
                    .Where(x => x.EnvironmentId == environmentId);
            }

            if (dashboardItemResources.Any(x => GetSemanticVersionOrNull(x.ReleaseVersion)?.SpecialVersion == branch))
            {
                dashboardItemResources = dashboardItemResources
                    .Where(x => GetSemanticVersionOrNull(x.ReleaseVersion)?.SpecialVersion == branch);
            }

            var dashboardItemResource = dashboardItemResources
                .FirstOrDefault();

            return dashboardItemResource;
        }

        private SemanticVersion GetSemanticVersionOrNull(string versionString)
        {
            SemanticVersion version;
            SemanticVersion.TryParse(versionString, out version);
            return version;
        }

        /// <summary>
        /// Get branches for any release
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        private List<Branch> GetBranchesForProject(string projectId)
        {
            var branches = from r in GetReleaseResources(projectId)
                let version = GetSemanticVersionOrNull(r.Version)
                where version != null
                select new Branch
                {
                    Name = version.SpecialVersion,
                    Id = version.SpecialVersion
                };

            return branches.ToList();
        }

        /// <summary>
        /// Get component for a branch. If release does not exist for branch then master branch is assumed.
        /// </summary>
        /// <param name="environmentId"></param>
        /// <param name="projectId"></param>
        /// <param name="branch"></param>
        /// <param name="dashboard"></param>
        /// <param name="componentFilter"></param>
        /// <returns></returns>
        private Component GetComponentForBranch(DashboardResource dashboard, string environmentId, string projectId, string branch, ComponentFilter componentFilter)
        {
            var releaseResources = GetReleaseResources(projectId);

            if (branch == null || branch.Equals("Master", StringComparison.InvariantCultureIgnoreCase))
            {
                branch = string.Empty;
            }

            //If we dont have any release in a branch assume master
            if (!string.IsNullOrEmpty(branch) && releaseResources.All(x => GetSemanticVersionOrNull(x.Version)?.SpecialVersion != branch))
            {
                branch = string.Empty;
            }

            //Get release for branch
            var releaseResource = releaseResources.FirstOrDefault(x => GetSemanticVersionOrNull(x.Version)?.SpecialVersion == branch);
            if (releaseResource == null) return null;
            
            // No other way to calculate duration otherwise :<
            var dashboardItemResource = GetClosestMatchingDashboardItemResource(dashboard, environmentId, projectId, branch);

            var healthy = dashboardItemResource != null && dashboardItemResource.State == TaskState.Success;

            var componentDeployedOnEnvironmentFromDuration = dashboardItemResource == null 
                ? null 
                : dashboardItemResource.CompletedTime - dashboardItemResource.QueueTime;
            
            var projectVariables = _repository.VariableSets.Get(releaseResource.ProjectVariableSetSnapshotId);

            var componentDependancies = projectVariables.Variables
                .Where(x => x.Name == ComponentDependancies)
                .SelectMany(x => JsonConvert.DeserializeObject<string[]>(x.Value))
                .Where(x => componentFilter == null || componentFilter.Match(x))
                .ToList();

            var component = new Component
            {
                Healthy = healthy,
                Version = new SemVer(releaseResource.Version),
                DeploymentDuration = componentDeployedOnEnvironmentFromDuration,
                Dependancies = componentDependancies
            };

            return component;
        }

        /// <summary>
        /// Get a component for an environment
        /// </summary>
        /// <param name="dashboard"></param>
        /// <param name="environmentId"></param>
        /// <param name="projectId"></param>
        /// <param name="componentFilter"></param>
        /// <returns></returns>
        private Component GetComponentForEnvironment(DashboardResource dashboard, string environmentId, string projectId, ComponentFilter componentFilter)
        {
            var dashboardItemResources = dashboard.Items
                .Where(x => x.EnvironmentId == environmentId && x.ProjectId == projectId);

            var dashboardItemResource = dashboardItemResources.FirstOrDefault();

            if (dashboardItemResource == null) return null;

            var componentDeployedOnEnvironmentFromDuration = dashboardItemResource.CompletedTime - dashboardItemResource.QueueTime;

            var healthy = dashboardItemResource.State == TaskState.Success;
            
            var release = _repository.Releases.Get(dashboardItemResource.ReleaseId);
            var projectVariables = _repository.VariableSets.Get(release.ProjectVariableSetSnapshotId);

            var componentDependancies = projectVariables.Variables
                .Where(x => x.Name == ComponentDependancies)
                .SelectMany(x => JsonConvert.DeserializeObject<string[]>(x.Value))
                .Where(x => componentFilter == null || componentFilter.Match(x))
                .ToList();

            var component = new Component
            {
                Healthy = healthy,
                Version = new SemVer(dashboardItemResource.ReleaseVersion),
                DeploymentDuration = componentDeployedOnEnvironmentFromDuration,
                Dependancies = componentDependancies
            };

            return component;
        }

        private ComponentDeploymentPlan GetEnvironmentDeploymentPlan(string projectId, string projectName, Component componentFrom, Component componentTo)
        {
            var deploymentPlan = new ComponentDeploymentPlan
            {
                Name = projectName,
                Id = projectId,
                ComponentFrom = componentFrom,
                ComponentTo = componentTo
            };

            if (componentFrom == null && componentTo == null)
            {
                deploymentPlan.Action = PlanAction.Skip;
            }
            else if (componentFrom == null)
            {
                deploymentPlan.Action = PlanAction.Remove;
            }
            else if (componentTo == null)
            {
                deploymentPlan.Action = PlanAction.Change;
            }
            else if (componentFrom.Version == componentTo.Version && componentTo.Healthy)
            {
                deploymentPlan.Action = PlanAction.Skip;
            }
            else
            {
                deploymentPlan.Action = PlanAction.Change;
            }

            return deploymentPlan;
        }

        public List<DeploymentPlanner.Environment> GetEnvironments()
        {
            var environments = _repository.Environments
                .GetAll()
                .Select(x => new DeploymentPlanner.Environment()
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .DistinctBy(x => x.Id)
                .OrderBy(b => b.Name)
                .ToList();

            return environments;
        }

        public List<Branch> GetBranches()
        {
            var branches = new List<Branch>();

            var projectIds = _repository.Projects.GetAll().Select(x=>x.Id);

            foreach (var projectId in projectIds)
            {
                if (_repository.Projects.Get(projectId).IsDisabled)
                {
                    continue;
                }
                branches.AddRange(GetBranchesForProject(projectId));
            }

            branches = branches
                .DistinctBy(x => x.Id)
                .OrderBy(b => b.Name)
                .ToList();

            return branches;
        }

        public EnvironmentDeploymentPlans GetEnvironmentMirrorDeploymentPlans(string environmentFrom, string environmentTo, ComponentFilter componentFilter = null)
        {
            var environments = environmentFrom == environmentTo ? 
                new[] {environmentFrom} : 
                new[] {environmentFrom, environmentTo};

            var environmentIds = _repository.Environments
                .GetAll()
                .Where(x=>environments.Contains(x.Name))
                .ToArray();

            var environmentReferenceFrom = environmentIds.FirstOrDefault(x => x.Name == environmentFrom);
            if (environmentReferenceFrom == null)
            {
                throw new Exception(string.Format("Unable to find environment - {0}", environmentFrom));
            }

            var environmentReferenceTo = environmentIds.FirstOrDefault(x => x.Name == environmentTo);
            if (environmentReferenceTo == null)
            {
                throw new Exception(string.Format("Unable to find environment - {0}", environmentTo));
            }

            var dashboard = _repository.Dashboards.GetDynamicDashboard(null, environmentIds.Select(x=>x.Id).ToArray());

            var environmentDeploymentPlan = new EnvironmentDeploymentPlans
            {
                EnvironmentFromId = environmentReferenceFrom.Id,
                EnvironmentToId = environmentReferenceTo.Id,
                EnvironmentDeploymentPlan = new EnvironmentDeploymentPlan(new List<ComponentDeploymentPlan>())
            };

            foreach (var dashboardProjectResource in dashboard.Projects)
            {
                if (_repository.Projects.Get(dashboardProjectResource.Id).IsDisabled)
                {
                    continue;
                }

                var projectName = dashboardProjectResource.Name;

                if (componentFilter != null && !componentFilter.Match(projectName))
                {
                    continue;
                }

                var projectId = dashboardProjectResource.Id;

                var componentFrom = GetComponentForEnvironment(dashboard, environmentReferenceFrom.Id, projectId, componentFilter);                

                var componentTo = environmentReferenceFrom.Id == environmentReferenceTo.Id 
                    ? componentFrom
                    : GetComponentForEnvironment(dashboard, environmentReferenceTo.Id, projectId, componentFilter);

                var deploymentPlan = GetEnvironmentDeploymentPlan(projectId, projectName, componentFrom, componentTo);

                environmentDeploymentPlan.EnvironmentDeploymentPlan.DeploymentPlans.Add(deploymentPlan);
            }

            return environmentDeploymentPlan;
        }


        public BranchDeploymentPlans GetBranchDeploymentPlans(string environment, string branch, ComponentFilter componentFilter = null)
        {
            var environments = new[] { environment };

            var environmentIds = _repository.Environments
                .GetAll()
                .Where(x => environments.Contains(x.Name))
                .ToArray();

            var environmentReference = environmentIds.FirstOrDefault(x => x.Name == environment);
            if (environmentReference == null)
            {
                throw new Exception(string.Format("Unable to find environment - {0}", environment));
            }
            
            var dashboard = _repository.Dashboards.GetDynamicDashboard(null, environmentIds.Select(x=>x.Id).ToArray());

            var branchDeploymentPlan = new BranchDeploymentPlans
            {
                EnvironmentId = environmentReference.Id,
                Branch = branch,
                EnvironmentDeploymentPlan = new EnvironmentDeploymentPlan(new List<ComponentDeploymentPlan>())
            };

            foreach (var dashboardProjectResource in dashboard.Projects)
            {
                if (_repository.Projects.Get(dashboardProjectResource.Id).IsDisabled)
                {
                    continue;
                }

                var projectName = dashboardProjectResource.Name;

                if (componentFilter != null && !componentFilter.Match(projectName))
                {
                    continue;
                }

                var projectId = dashboardProjectResource.Id;

                var componentFrom = GetComponentForBranch(dashboard, environmentReference.Id, projectId, branch, componentFilter);
                var componentTo = GetComponentForEnvironment(dashboard, environmentReference.Id, projectId, componentFilter);

                var deploymentPlan = GetEnvironmentDeploymentPlan(projectId, projectName, componentFrom, componentTo);

                branchDeploymentPlan.EnvironmentDeploymentPlan.DeploymentPlans.Add(deploymentPlan);
            }

            return branchDeploymentPlan;
        }

        public RedeployDeploymentPlans GetRedeployDeploymentPlans(string environment, ComponentFilter componentFilter = null)
        {
            var environments = new[] { environment };

            var environmentIds = _repository.Environments
                .GetAll()
                .Where(x => environments.Contains(x.Name))
                .ToArray();

            var environmentReference = environmentIds.FirstOrDefault(x => x.Name == environment);
            if (environmentReference == null)
            {
                throw new Exception(string.Format("Unable to find environment - {0}", environment));
            }

            var dashboard = _repository.Dashboards.GetDynamicDashboard(null, environmentIds.Select(x=>x.Id).ToArray());

            var redeployDeploymentPlans = new RedeployDeploymentPlans
            {
                EnvironmentId = environmentReference.Id,
                EnvironmentDeploymentPlan = new EnvironmentDeploymentPlan(new List<ComponentDeploymentPlan>())
            };

            foreach (var dashboardProjectResource in dashboard.Projects)
            {
                if (_repository.Projects.Get(dashboardProjectResource.Id).IsDisabled)
                {
                    continue;
                }

                var projectName = dashboardProjectResource.Name;

                if (componentFilter != null && !componentFilter.Match(projectName))
                {
                    continue;
                }

                var projectId = dashboardProjectResource.Id;

                var componentFrom = GetComponentForEnvironment(dashboard, environmentReference.Id, projectId, componentFilter);
                var componentTo = componentFrom;

                var deploymentPlan = GetEnvironmentDeploymentPlan(projectId, projectName, componentFrom, componentTo);

                //If the component is installed. Then re-install it
                if (componentFrom != null)
                {
                    deploymentPlan.Action = PlanAction.Change;
                }

                redeployDeploymentPlans.EnvironmentDeploymentPlan.DeploymentPlans.Add(deploymentPlan);
            }

            return redeployDeploymentPlans;
        }
    }
}
