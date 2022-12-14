using CommandLine;

namespace OctopusPuppet.Cmd
{
    [Verb("Redeployment", HelpText = "Re-install all components in environment.")]
    class RedploymentOptions
    {
        [Option("OctopusUrl",
            Required = true,
            SetName = "Redeployment",
            HelpText = "The url to the octopus server e.g. 'http://octopus.test.com/'")]
        public string OctopusUrl { get; set; }

        [Option("OctopusApiKey",
            Required = true,
            SetName = "Redeployment",
            HelpText = "The api key for the octopus server e.g. 'API-HAAAS4MM6YBBSAIQVVHCQQUEA0'")]
        public string OctopusApiKey { get; set; }

        [Option("ComponentFilterPath",
            Default = "",
            SetName = "Redeployment",
            HelpText = "Component filter path.")]
        public string ComponentFilterPath { get; set; }

        [Option("ComponentFilter",
            Default = "",
            SetName = "Redeployment",
            HelpText = "Component filter json base64 encoded.")]
        public string ComponentFilter { get; set; }

        [Option("TargetEnvironment",
            Required = true,
            SetName = "Redeployment",
            HelpText = "The environment to deploy to.")]
        public string TargetEnvironment { get; set; }

        [Option('d', "Deploy",
            Default = false,
            SetName = "Redeployment",
            HelpText = "Deploy")]
        public bool Deploy { get; set; }

        [Option('u', "UpdateVariables",
             Default = false,
             SetName = "Redeployment",
             HelpText = "Update variables")]
        public bool UpdateVariables { get; set; }

        [Option('s', "HideDeploymentProgress",
            Default = false,
            SetName = "Redeployment",
            HelpText = "Hide deployment progress")]
        public bool HideDeploymentProgress { get; set; }

        [Option('p', "MaximumParallelDeployments",
            Default = 2,
            SetName = "Redeployment",
            HelpText = "Maximum parallel deployments")]
        public int MaximumParalleDeployments { get; set; }

        [Option("EnvironmentDeploymentPath",
            Default = "",
            SetName = "Redeployment",
            HelpText = "Environment Deployment path to save to.")]
        public string EnvironmentDeploymentPath { get; set; }

        [Option("Teamcity",
            Default = false,
            SetName = "Redeployment",
            HelpText = "Use Teamcity output")]
        public bool Teamcity { get; set; }
    }
}