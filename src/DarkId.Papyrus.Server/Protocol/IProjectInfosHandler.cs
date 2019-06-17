using DarkId.Papyrus.LanguageService.External;
using OmniSharp.Extensions.Embedded.MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Server.Protocol
{
    public class ProjectInfosCapability : DynamicCapability
    {

    }

    public class ProjectInfoScript
    {
        public string Identifier { get; set; }
        public string FilePath { get; set; }
    }

    public class ProjectInfoSourceInclude
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public Container<ProjectInfoScript> Scripts { get; set; }
    }

    public class ProjectInfo
    {
        public string Name { get; set; }
        public Container<ProjectInfoSourceInclude> SourceIncludes { get; set; }
    }

    public class ProjectInfos
    {
        public Container<ProjectInfo> Projects { get; set; }
    }

    public class ProjectInfosParams : IRequest<ProjectInfos>, IBaseRequest
    {

    }

    [Method("papyrus/projectInfos")]
    [Parallel]
    public interface IProjectInfosHandler :
        IJsonRpcRequestHandler<ProjectInfosParams, ProjectInfos>,
        IRequestHandler<ProjectInfosParams, ProjectInfos>,
        IJsonRpcHandler,
        ICapability<ProjectInfosCapability>
    {
    }
}
