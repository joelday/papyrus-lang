// Copyright (c) 2018 Samsung Electronics Co., LTD
// Distributed under the MIT License.
// See the LICENSE file in the project root for more information.
#pragma once

#include <fstream>
#include <mutex>
#include <string>

#include "nlohmann/json.hpp"
#include "debugger.h"
#include <queue>


class VSCodeProtocol : public Protocol
{
	static const std::string LOG_COMMAND;
	static const std::string LOG_RESPONSE;
	static const std::string LOG_EVENT;

	std::mutex m_outMutex;
	std::mutex m_inMutex;

	std::function<void(std::string)> m_sendCallback;

	std::queue<std::string>* m_inputQueue;

	uint64_t m_seqCounter;

	std::string m_fileExec;
	std::vector<std::string> m_execArgs;

	void AddCapabilitiesTo(nlohmann::json &capabilities);
	void EmitEvent(const std::string &name, const nlohmann::json &body);
	HRESULT HandleCommand(const std::string &command, const nlohmann::json &arguments, nlohmann::json &body);

	void Log(const std::string &prefix, const std::string &text);

public:

	VSCodeProtocol(std::function<void(std::string)> sendCallback) : Protocol(), m_seqCounter(1), m_sendCallback(sendCallback) {
		m_inputQueue = new std::queue<std::string>();
	}

	void OverrideLaunchCommand(const std::string &fileExec, const std::vector<std::string> &args)
	{
		m_fileExec = fileExec;
		m_execArgs = args;
	}

	void Exit();

	void Receive(std::string message);

	void EmitInitializedEvent() override;
	void EmitStoppedEvent(StoppedEvent event) override;
	void EmitExitedEvent(ExitedEvent event) override;
	void EmitTerminatedEvent() override;
	void EmitContinuedEvent(ContinuedEvent event) override;
	void EmitThreadEvent(ThreadEvent event) override;
	void EmitModuleEvent(ModuleEvent event) override;
	void EmitLoadedSourceEvent(LoadedSourceEvent event) override;
	void EmitOutputEvent(OutputEvent event) override;
	void EmitBreakpointEvent(BreakpointEvent event) override;
	void Cleanup() override;
	void CommandLoop() override;

	void EmitCapabilitiesEvent();
};
