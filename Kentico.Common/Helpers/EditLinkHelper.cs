
using Kentico.Kontent.Management.Helpers;
using Kentico.Kontent.Management.Helpers.Configuration;

namespace Kentico.Common.Helpers
{
    public  class EditLinkHelper
    {
        private static EditLinkHelper _instance = null;
        private static readonly object padlock = new object();
        public EditLinkBuilder Builder { get; private set; }

        public EditLinkHelper()
        { 
        }

        public EditLinkHelper(string projectId)
        {
            var linkBuilderoptions = new ManagementHelpersOptions() { ProjectId = projectId };
            Builder = new EditLinkBuilder(linkBuilderoptions);
        }

        public static EditLinkHelper Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (padlock)
                    {
                        if (_instance == null)
                        {
                            _instance = new EditLinkHelper();
                        }
                    }
                }
                return _instance;
            }
        }
    }
}