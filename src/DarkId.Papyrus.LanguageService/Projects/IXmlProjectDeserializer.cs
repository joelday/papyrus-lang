using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace DarkId.Papyrus.LanguageService.Projects
{
    public interface IXmlProjectDeserializer
    {
        PapyrusProject DeserializeProject(string xmlText);
    }
}