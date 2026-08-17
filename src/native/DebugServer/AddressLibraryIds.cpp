#include "AddressLibraryIds.h"

#include <algorithm>
#include <cctype>
#include <fstream>
#include <iterator>
#include <limits>
#include <sstream>
#include <stdexcept>
#include <utility>
#include <variant>

namespace DarkId::Papyrus::DebugServer
{
	namespace
	{
		bool g_loaded = false;
		std::filesystem::path g_path;
		std::string g_game;
		std::string g_version;
		std::string g_flavor;
		std::unordered_map<std::string, std::uint64_t> g_ids;

		using FlavorMap = std::unordered_map<std::string, std::uint64_t>;
		using IdValue = std::variant<std::uint64_t, FlavorMap>;

		std::string Trim(std::string s)
		{
			const auto notSpace = [](unsigned char c) { return !std::isspace(c); };
			s.erase(s.begin(), std::find_if(s.begin(), s.end(), notSpace));
			s.erase(std::find_if(s.rbegin(), s.rend(), notSpace).base(), s.end());
			return s;
		}

		std::string NormalizeVersion(std::string_view raw)
		{
			std::string s = Trim(std::string(raw));
			for (char& c : s)
			{
				if (c == '-' || c == '_')
				{
					c = '.';
				}
			}
			return s;
		}

		std::vector<std::string> VersionCandidates(std::string_view raw)
		{
			std::vector<std::string> out;
			auto add = [&](std::string value) {
				value = Trim(std::move(value));
				if (value.empty())
				{
					return;
				}
				if (std::find(out.begin(), out.end(), value) == out.end())
				{
					out.push_back(std::move(value));
				}
			};

			add(std::string(raw));
			const std::string normalized = NormalizeVersion(raw);
			add(normalized);

			std::string cur = normalized;
			while (cur.size() > 2 && cur.ends_with(".0"))
			{
				cur.resize(cur.size() - 2);
				add(cur);
			}
			return out;
		}

		std::vector<std::string> FlavorCandidates(std::string_view flavor)
		{
			std::vector<std::string> out;
			if (!flavor.empty())
			{
				out.emplace_back(flavor);
			}
			// Current plugin used SE Address Library IDs as the VR fallback.
			if (flavor == "vr")
			{
				out.emplace_back("se");
			}
			return out;
		}

		std::string JoinQuoted(const std::vector<std::string>& values)
		{
			std::ostringstream os;
			for (std::size_t i = 0; i < values.size(); ++i)
			{
				if (i != 0)
				{
					os << ", ";
				}
				os << '\'' << values[i] << '\'';
			}
			return os.str();
		}

		// Enough JSON for `{ "name": 1, "other": { "se": 2, "ae": 3 } }`.
		struct IdJsonParser
		{
			std::string_view text;
			std::size_t i = 0;
			std::string error;

			void SkipWs()
			{
				while (i < text.size() && std::isspace(static_cast<unsigned char>(text[i])))
				{
					++i;
				}
			}

			bool Fail(std::string message)
			{
				error = std::move(message);
				return false;
			}

			bool Match(char c)
			{
				SkipWs();
				if (i < text.size() && text[i] == c)
				{
					++i;
					return true;
				}
				return false;
			}

			bool Expect(char c, const char* what)
			{
				if (!Match(c))
				{
					return Fail(std::string("expected ") + what);
				}
				return true;
			}

			bool ParseString(std::string& out)
			{
				SkipWs();
				if (i >= text.size() || text[i] != '"')
				{
					return Fail("expected string");
				}
				++i;
				out.clear();
				while (i < text.size())
				{
					const char c = text[i++];
					if (c == '"')
					{
						return true;
					}
					if (c == '\\')
					{
						if (i >= text.size())
						{
							return Fail("unterminated string escape");
						}
						const char esc = text[i++];
						switch (esc)
						{
						case '"':
						case '\\':
						case '/':
							out.push_back(esc);
							break;
						case 'b':
							out.push_back('\b');
							break;
						case 'f':
							out.push_back('\f');
							break;
						case 'n':
							out.push_back('\n');
							break;
						case 'r':
							out.push_back('\r');
							break;
						case 't':
							out.push_back('\t');
							break;
						default:
							return Fail("unsupported string escape");
						}
						continue;
					}
					out.push_back(c);
				}
				return Fail("unterminated string");
			}

			bool ParseNumber(std::uint64_t& out)
			{
				SkipWs();
				if (i < text.size() && text[i] == '-')
				{
					return Fail("Address Library IDs must be non-negative");
				}
				if (i >= text.size() || !std::isdigit(static_cast<unsigned char>(text[i])))
				{
					return Fail("expected number");
				}
				std::uint64_t value = 0;
				while (i < text.size() && std::isdigit(static_cast<unsigned char>(text[i])))
				{
					const std::uint64_t digit = static_cast<std::uint64_t>(text[i] - '0');
					if (value > (std::numeric_limits<std::uint64_t>::max() - digit) / 10)
					{
						return Fail("number is out of range");
					}
					value = value * 10 + digit;
					++i;
				}
				out = value;
				return true;
			}

			bool ParseFlavorObject(FlavorMap& out)
			{
				if (!Expect('{', "'{'"))
				{
					return false;
				}
				out.clear();
				SkipWs();
				if (Match('}'))
				{
					return true;
				}
				for (;;)
				{
					std::string key;
					if (!ParseString(key))
					{
						return false;
					}
					if (!Expect(':', "':'"))
					{
						return false;
					}
					std::uint64_t value = 0;
					if (!ParseNumber(value))
					{
						return false;
					}
					out.emplace(std::move(key), value);
					if (Match(','))
					{
						continue;
					}
					if (Match('}'))
					{
						return true;
					}
					return Fail("expected ',' or '}' in ID object");
				}
			}

			bool ParseRoot(std::unordered_map<std::string, IdValue>& out)
			{
				if (!Expect('{', "a JSON object"))
				{
					return false;
				}
				out.clear();
				SkipWs();
				if (Match('}'))
				{
					return true;
				}
				for (;;)
				{
					std::string key;
					if (!ParseString(key))
					{
						return false;
					}
					if (!Expect(':', "':'"))
					{
						return false;
					}
					SkipWs();
					IdValue value;
					if (i < text.size() && text[i] == '{')
					{
						FlavorMap flavors;
						if (!ParseFlavorObject(flavors))
						{
							return false;
						}
						value = std::move(flavors);
					}
					else
					{
						std::uint64_t number = 0;
						if (!ParseNumber(number))
						{
							return false;
						}
						value = number;
					}
					out.emplace(std::move(key), std::move(value));
					if (Match(','))
					{
						continue;
					}
					if (Match('}'))
					{
						SkipWs();
						return true;
					}
					return Fail("expected ',' or '}' in root object");
				}
			}
		};

		bool ResolveId(
			const IdValue& value,
			std::string_view name,
			std::string_view flavor,
			const std::filesystem::path& file,
			std::uint64_t& out,
			AddressLibraryIds::LoadError& error)
		{
			if (const auto* number = std::get_if<std::uint64_t>(&value))
			{
				out = *number;
				return true;
			}

			const auto& flavors = std::get<FlavorMap>(value);
			for (const auto& candidate : FlavorCandidates(flavor))
			{
				const auto it = flavors.find(candidate);
				if (it != flavors.end())
				{
					out = it->second;
					return true;
				}
			}

			std::vector<std::string> available;
			available.reserve(flavors.size());
			for (const auto& [key, _] : flavors)
			{
				available.push_back(key);
			}
			std::sort(available.begin(), available.end());

			error.message = "Papyrus Debug Server: Address Library ID '" + std::string(name) + "' in " +
				file.string() + " has no '" + std::string(flavor) + "' value";
			if (!available.empty())
			{
				error.message += " (available: " + JoinQuoted(available) + ")";
			}
			error.message += ".";
			return false;
		}

		std::string FormatMissingVersionError(
			std::string_view game,
			std::string_view version,
			std::string_view flavor,
			const std::vector<std::filesystem::path>& candidates,
			const std::vector<std::filesystem::path>& searchDirectories)
		{
			std::ostringstream os;
			os << "Papyrus Debug Server does not support " << game << ' ' << version;
			if (!flavor.empty())
			{
				os << " (flavor " << flavor << ")";
			}
			os << ".\nLooked for:\n";
			for (const auto& candidate : candidates)
			{
				os << "  " << candidate.string() << '\n';
			}

			std::vector<std::string> available;
			for (const auto& dir : searchDirectories)
			{
				if (!std::filesystem::exists(dir))
				{
					continue;
				}
				auto files = AddressLibraryIds::AvailableFiles(dir);
				available.insert(available.end(), files.begin(), files.end());
			}
			std::sort(available.begin(), available.end());
			available.erase(std::unique(available.begin(), available.end()), available.end());

			if (available.empty())
			{
				os << "No versioned Address Library JSON files were found in the search path.";
			}
			else
			{
				os << "Available version files:\n";
				for (const auto& file : available)
				{
					os << "  " << file << '\n';
				}
			}
			os << "A missing or mismatched game version cannot be hooked safely. "
				  "Add a versioned JSON file instead of guessing Address Library IDs.";
			return os.str();
		}
	}

	void AddressLibraryIds::Reset() noexcept
	{
		g_loaded = false;
		g_path.clear();
		g_game.clear();
		g_version.clear();
		g_flavor.clear();
		g_ids.clear();
	}

	bool AddressLibraryIds::IsLoaded() noexcept
	{
		return g_loaded;
	}

	const std::filesystem::path& AddressLibraryIds::LoadedPath()
	{
		return g_path;
	}

	const std::string& AddressLibraryIds::LoadedGame()
	{
		return g_game;
	}

	const std::string& AddressLibraryIds::LoadedVersion()
	{
		return g_version;
	}

	const std::string& AddressLibraryIds::LoadedFlavor()
	{
		return g_flavor;
	}

	const std::unordered_map<std::string, std::uint64_t>& AddressLibraryIds::All()
	{
		return g_ids;
	}

	bool AddressLibraryIds::TryGet(std::string_view name, std::uint64_t& out) noexcept
	{
		if (!g_loaded)
		{
			return false;
		}
		const auto it = g_ids.find(std::string(name));
		if (it == g_ids.end())
		{
			return false;
		}
		out = it->second;
		return true;
	}

	std::uint64_t AddressLibraryIds::Get(std::string_view name)
	{
		if (!g_loaded)
		{
			throw std::runtime_error(
				"Papyrus Debug Server: Address Library IDs were not loaded; cannot resolve '" +
				std::string(name) + "'.");
		}

		std::uint64_t value = 0;
		if (!TryGet(name, value))
		{
			throw std::runtime_error(
				"Papyrus Debug Server: Address Library ID '" + std::string(name) +
				"' is not present in " + g_path.string() + ".");
		}
		return value;
	}

	std::vector<std::string> AddressLibraryIds::AvailableFiles(const std::filesystem::path& dataDirectory)
	{
		std::vector<std::string> files;
		std::error_code ec;
		if (!std::filesystem::exists(dataDirectory, ec) || !std::filesystem::is_directory(dataDirectory, ec))
		{
			return files;
		}

		for (const auto& entry : std::filesystem::directory_iterator(dataDirectory, ec))
		{
			if (ec)
			{
				break;
			}
			if (entry.is_regular_file() && entry.path().extension() == ".json")
			{
				files.push_back(entry.path().filename().string());
			}
		}
		std::sort(files.begin(), files.end());
		return files;
	}

	bool AddressLibraryIds::Load(
		std::string_view game,
		std::string_view version,
		std::string_view flavor,
		const std::filesystem::path& dataDirectory,
		const std::vector<std::string_view>& requiredIds,
		LoadError& error)
	{
		return Load(game, version, flavor, std::vector<std::filesystem::path>{ dataDirectory }, requiredIds, error);
	}

	bool AddressLibraryIds::Load(
		std::string_view game,
		std::string_view version,
		std::string_view flavor,
		const std::vector<std::filesystem::path>& searchDirectories,
		const std::vector<std::string_view>& requiredIds,
		LoadError& error)
	{
		Reset();
		error.message.clear();

		if (game.empty())
		{
			error.message = "Papyrus Debug Server: Address Library load requires a game name (e.g. skyrimse, fallout4).";
			return false;
		}
		if (version.empty())
		{
			error.message = "Papyrus Debug Server: Address Library load requires a runtime version string.";
			return false;
		}

		const auto versions = VersionCandidates(version);
		std::vector<std::filesystem::path> candidates;
		for (const auto& dir : searchDirectories)
		{
			for (const auto& ver : versions)
			{
				candidates.push_back(dir / (std::string(game) + "-" + ver + ".json"));
			}
		}

		std::filesystem::path found;
		for (const auto& candidate : candidates)
		{
			std::error_code ec;
			if (std::filesystem::is_regular_file(candidate, ec))
			{
				found = candidate;
				break;
			}
		}

		if (found.empty())
		{
			error.message = FormatMissingVersionError(game, version, flavor, candidates, searchDirectories);
			return false;
		}

		std::ifstream in(found);
		if (!in)
		{
			error.message = "Papyrus Debug Server: could not open Address Library data file " + found.string() + ".";
			return false;
		}

		const std::string text{ std::istreambuf_iterator<char>(in), std::istreambuf_iterator<char>() };
		IdJsonParser parser;
		parser.text = text;
		std::unordered_map<std::string, IdValue> root;
		if (!parser.ParseRoot(root))
		{
			error.message = "Papyrus Debug Server: failed to parse Address Library data " + found.string() +
				": " + parser.error + ".";
			return false;
		}

		std::unordered_map<std::string, std::uint64_t> ids;
		for (const auto& [name, value] : root)
		{
			std::uint64_t id = 0;
			if (!ResolveId(value, name, flavor, found, id, error))
			{
				return false;
			}
			ids.emplace(name, id);
		}

		for (const auto required : requiredIds)
		{
			if (ids.find(std::string(required)) == ids.end())
			{
				error.message = "Papyrus Debug Server: Address Library data " + found.string() +
					" is missing required ID '" + std::string(required) + "'.";
				return false;
			}
		}

		g_ids = std::move(ids);
		g_path = found;
		g_game = std::string(game);
		g_version = NormalizeVersion(version);
		g_flavor = std::string(flavor);
		g_loaded = true;
		return true;
	}
}
