﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OctopusPuppet.Scheduler;

namespace OctopusPuppet.Deployer
{
    public class DeploymentExecutor
    {
        private readonly IComponentVertexDeployer _componentVertexDeployer;
        private readonly EnvironmentDeployment _environmentDeployment;
        private readonly CancellationToken _cancellationToken;
        private readonly IProgress<ComponentVertexDeploymentProgress> _progress;

        public DeploymentExecutor(IComponentVertexDeployer componentVertexDeployer, EnvironmentDeployment environmentDeployment, CancellationToken cancellationToken, IProgress<ComponentVertexDeploymentProgress> progress)
        {
            _componentVertexDeployer = componentVertexDeployer;
            _environmentDeployment = environmentDeployment;
            _cancellationToken = cancellationToken;
            _progress = progress;
        }

        public async Task Execute()
        {
            await DeployEnvironment(_environmentDeployment, _cancellationToken);
        }

        /// <summary>
        /// Deploy all products in parallel
        /// </summary>
        /// <param name="environmentDeployment"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>True if all products were deployed successfully; else false</returns>
        private async Task<bool> DeployEnvironment(EnvironmentDeployment environmentDeployment, CancellationToken cancellationToken)
        {
            var productTasks = environmentDeployment.ProductDeployments
                .Select(productDeployment => DeployProduct(productDeployment, cancellationToken));

            var productDeploymentResults = await WhenAll(productTasks, cancellationToken);

            return productDeploymentResults != null && !productDeploymentResults.Any(x => x != true);
        }

        /// <summary>
        /// Deploy product steps in series
        /// </summary>
        /// <param name="productDeployment"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>True if all product steps were deployed successfully; else false</returns>
        private async Task<bool> DeployProduct(ProductDeployment productDeployment, CancellationToken cancellationToken)
        {
            var productSteps = productDeployment.DeploymentSteps
                .OrderBy(x => x.ExecutionOrder);

            foreach (var productDeploymentStep in productSteps)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                var deployProductStepResults = await DeployProductStep(productDeploymentStep, cancellationToken);

                if (!deployProductStepResults)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Deploy all components in a product step in parallel
        /// </summary>
        /// <param name="productDeploymentStep"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>True if all components were deployed successfully; else false</returns>
        private async Task<bool> DeployProductStep(ProductDeploymentStep productDeploymentStep, CancellationToken cancellationToken)
        {
            var componentDeploymentForProductDeploymentStepTasks = productDeploymentStep.ComponentDeployments
                .Select(x=>x.Vertex)
                .OrderBy(x=>x.ExecutionOrder) //does not matter if other components finish before this one
                .Select(x => Task.Run(() => DeployComponent(x, cancellationToken), cancellationToken));

            var deployComponentResults = await WhenAll(componentDeploymentForProductDeploymentStepTasks, cancellationToken);

            return deployComponentResults != null && !deployComponentResults.Any(x => x != ComponentVertexDeploymentStatus.Success);
        }

        /// <summary>
        /// Deploy component
        /// </summary>
        /// <param name="componentDeploymentVertex"></param>
        /// <param name="cancellationToken"></param>
        private ComponentVertexDeploymentStatus DeployComponent(ComponentDeploymentVertex componentDeploymentVertex, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return ComponentVertexDeploymentStatus.Cancelled;
            }

            //Start progress
            if (_progress != null)
            {
                _progress.Report(new ComponentVertexDeploymentProgress
                {
                    Vertex = componentDeploymentVertex,
                    Status = ComponentVertexDeploymentStatus.Started,
                    MinimumValue = 0,
                    MaximumValue =
                        componentDeploymentVertex.DeploymentDuration.HasValue
                            ? componentDeploymentVertex.DeploymentDuration.Value.Ticks
                            : 0,
                    Value = 0,
                    Text = "Started"
                });
            }

            var status = _componentVertexDeployer.Deploy(componentDeploymentVertex, cancellationToken, _progress);

            //Finish progress    
            if (_progress != null)
            {
                _progress.Report(new ComponentVertexDeploymentProgress
                {
                    Vertex = componentDeploymentVertex,
                    Status = status,
                    MinimumValue = 0,
                    MaximumValue =
                        componentDeploymentVertex.DeploymentDuration.HasValue
                            ? componentDeploymentVertex.DeploymentDuration.Value.Ticks
                            : 0,
                    Value =
                        componentDeploymentVertex.DeploymentDuration.HasValue
                            ? componentDeploymentVertex.DeploymentDuration.Value.Ticks
                            : 0,
                    Text = status.ToString()
                });
            }

            return status;
        }

        private static async Task<TResult[]> WhenAll<TResult>(IEnumerable<Task<TResult>> tasks, CancellationToken cancellationToken)
        {
            var resultsForTasks = Task.WhenAll(tasks);

            if (await Task.WhenAny(resultsForTasks, cancellationToken.AsTask()) == resultsForTasks)
            {
                return await resultsForTasks;
            }

            return null;
        }
    }
}