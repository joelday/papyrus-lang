// using System;
// using System.IO;
// using System.Linq;
// using System.Text.RegularExpressions;
// using System.Threading.Tasks;
// using DarkId.Papyrus.Common;
// using DarkId.Papyrus.LanguageService.Program;
// using DarkId.Papyrus.LanguageService.Projects;
// using Microsoft.VisualStudio.TestTools.UnitTesting;

// namespace DarkId.Papyrus.Test.LanguageService.Program
// {
//     [TestClass]
//     public class ProgramUtilitiesTests
//     {
//         private readonly IFileSystem _fileSystem = new LocalFileSystem();

//         [TestMethod]
//         public async Task ResolveSourceFiles_ShouldResolveFiles()
//         {
//             var project = await new FileSystemXmlProjectLoader(_fileSystem, new XmlProjectDeserializer())
//                 .LoadProject("../../../../../papyrus/FO4TestScripts/Project/Project.ppj");

//             var programOptionsBuilder = new ProgramOptionsBuilder();
//             var programOptions = programOptionsBuilder.WithProject(project).Build();

//             var projectSourceFiles = await _fileSystem.ResolveSourceFiles(programOptions.Sources);

//             // TODO: Assertion
//         }
//     }
// }