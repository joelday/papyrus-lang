using System.IO;
using System.Xml.Serialization;

namespace DarkId.Papyrus.LanguageService.Projects
{
    public class XmlProjectDeserializer : IXmlProjectDeserializer
    {
        public PapyrusProject DeserializeProject(string xmlText)
        {
            using (var sr = new StringReader(xmlText))
            {
                var serializer = new XmlSerializer(typeof(PapyrusProject));
                var project = (PapyrusProject)serializer.Deserialize(sr);
                project.ExpandVariables();

                return project;
            }
        }
    }
}