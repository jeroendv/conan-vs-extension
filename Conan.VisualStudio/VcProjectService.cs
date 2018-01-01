using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Conan.VisualStudio.Core;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.VCProjectEngine;
using Task = System.Threading.Tasks.Task;

namespace Conan.VisualStudio
{
    internal static class VcProjectService
    {
        public static VCProject GetActiveProject()
        {
            var dte = (DTE)Package.GetGlobalService(typeof(SDTE));
            return GetActiveProject(dte);
        }

        private static VCProject GetActiveProject(DTE dte)
        {
            bool IsCppProject(Project project) =>
                project != null
                && (project.CodeModel.Language == CodeModelLanguageConstants.vsCMLanguageMC
                    || project.CodeModel.Language == CodeModelLanguageConstants.vsCMLanguageVC);

            var projects = (object[])dte.ActiveSolutionProjects;
            return projects.Cast<Project>().Where(IsCppProject).Select(p => p.Object).OfType<VCProject>().FirstOrDefault();
        }

        public static ConanProject ExtractConanConfiguration(VCProject project) => new ConanProject
        {
            Path = project.ProjectDirectory,
            InstallPath = Path.Combine(project.ProjectDirectory, "conan"),
            Compiler = "Visual Studio",
            CompilerVersion = "15"
        };

        public static Task AddPropsImport(string projectPath, string propFilePath) => Task.Run(() =>
        {
            var xml = XDocument.Load(projectPath);
            var project = xml.Root;
            if (project == null)
            {
                throw new Exception($"Project {projectPath} is malformed: no root element");
            }

            XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
            var import = ns + "Import";
            var existingImports = project.Descendants(import);
            if (existingImports.Any(node => node.Attribute("Project")?.Value == propFilePath))
            {
                return;
            }

            var newImport = new XElement(import);
            newImport.SetAttributeValue("Project", propFilePath);
            newImport.SetAttributeValue("Condition", $"Exists('{propFilePath}')");
            project.Add(newImport);

            xml.Save(projectPath);
        });
    }
}