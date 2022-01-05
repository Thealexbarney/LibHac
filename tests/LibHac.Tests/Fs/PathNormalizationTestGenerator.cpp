// Uses GLoat to run code in nnsdk https://github.com/h1k421/GLoat
#include <gloat.hpp>

#include<array>
#include<string>
#include<tuple>

static char Buf[0x80000];
static int BufPos = 0;

static char ResultNameBuf[0x100];

namespace nn::fs::detail {
    bool IsEnabledAccessLog();
}

// SDK 13
namespace nn::fs {
    bool IsSubPath(const char* path1, const char* path2);

    class PathFlags {
    private:
        int32_t value;
    public:
        PathFlags() { value = 0; }

        void AllowWindowsPath() { value |= (1 << 0); }
        void AllowRelativePath() { value |= (1 << 1); }
        void AllowEmptyPath() { value |= (1 << 2); }
        void AllowMountName() { value |= (1 << 3); }
        void AllowBackslash() { value |= (1 << 4); }
        void AllowAllCharacters() { value |= (1 << 5); }

        bool IsWindowsPathAllowed()const { return (value & (1 << 0)) != 0; }
        bool IsRelativePathAllowed()const { return (value & (1 << 1)) != 0; }
        bool IsEmptyPathAllowed()const { return (value & (1 << 2)) != 0; }
        bool IsMountNameAllowed()const { return (value & (1 << 3)) != 0; }
        bool IsBackslashAllowed()const { return (value & (1 << 4)) != 0; }
        bool AreAllCharactersAllowed()const { return (value & (1 << 5)) != 0; }
    };

    class Path {
    public:
        char* m_String;
        char* m_WriteBuffer;
        uint64_t m_UniquePtrLength;
        uint64_t m_WriteBufferLength;
        bool m_IsNormalized;

        Path();
        nn::Result Initialize(char const* path);
        nn::Result InitializeWithNormalization(char const* path);
        nn::Result InitializeWithReplaceUnc(char const* path);
        nn::Result Initialize(char const* path, uint64_t pathLength);
        nn::Result InsertParent(char const* path);
        nn::Result RemoveChild();
        nn::Result Normalize(const nn::fs::PathFlags&);
    };

    class PathFormatter {
    public:
        static nn::Result Normalize(char* buffer, uint64_t normalizeBufferLength, const char* path, uint64_t pathLength, const nn::fs::PathFlags&);
        static nn::Result IsNormalized(bool* outIsNormalized, uint64_t* outNormalizedPathLength, const char* path, const nn::fs::PathFlags&);
        static nn::Result SkipWindowsPath(const char** outPath, uint64_t* outLength, bool* outIsNormalized, const char* path, bool hasMountName);
        static nn::Result SkipMountName(const char** outPath, uint64_t* outLength, const char* path);
    };

    class PathNormalizer {
    public:
        static nn::Result Normalize(char* outBuffer, uint64_t* outLength, const char* path, uint64_t outBufferLength, bool isWindowsPath, bool isDriveRelative);
        static nn::Result Normalize(char* outBuffer, uint64_t* outLength, const char* path, uint64_t outBufferLength, bool isWindowsPath, bool isDriveRelative, bool allowAllCharacters);
        static nn::Result IsNormalized(bool* outIsNormalized, uint64_t* outNormalizedPathLength, const char* path);
        static nn::Result IsNormalized(bool* outIsNormalized, uint64_t* outNormalizedPathLength, const char* path, bool allowAllCharacters);
    };
}

template<typename T, typename... Ts>
constexpr auto make_array(T&& head, Ts&&... tail)->std::array<T, 1 + sizeof...(Ts)>
{
    return { head, tail ... };
}

template<size_t N, typename... Ts>
void CreateTest(const char* name, void (*func)(Ts...), const std::array<std::tuple<Ts...>, N>& testData) {
    Buf[0] = '\n';
    BufPos = 1;

    BufPos += sprintf(&Buf[BufPos], "%s\n", name);

    for (auto item : testData) {
        std::apply(func, item);
    }

    svcOutputDebugString(Buf, BufPos);
}

const char* GetResultName(nn::Result result) {
    switch (result.GetValue()) {
    case 0: return "Result.Success";
    case 0x177202: return "ResultFs.NotImplemented.Value";
    case 0x2EE402: return "ResultFs.InvalidPath.Value";
    case 0x2EE602: return "ResultFs.TooLongPath.Value";
    case 0x2EE802: return "ResultFs.InvalidCharacter.Value";
    case 0x2EEA02: return "ResultFs.InvalidPathFormat.Value";
    case 0x2EEC02: return "ResultFs.DirectoryUnobtainable.Value";
    default:
        sprintf(ResultNameBuf, "0x%x", result.GetValue());
        return ResultNameBuf;
    }
}

constexpr const char* const BoolStr(bool value)
{
    return value ? "true" : "false";
}

nn::fs::PathFlags GetPathFlags(char const* pathFlags) {
    nn::fs::PathFlags flags = nn::fs::PathFlags();

    for (char const* c = pathFlags; *c; c++) {
        switch (*c) {
        case 'B':
            flags.AllowBackslash();
            break;
        case 'E':
            flags.AllowEmptyPath();
            break;
        case 'M':
            flags.AllowMountName();
            break;
        case 'R':
            flags.AllowRelativePath();
            break;
        case 'W':
            flags.AllowWindowsPath();
            break;
        case 'C':
            flags.AllowAllCharacters();
            break;
        }
    }

    return flags;
}

// Escape single double-quotes to double double-quotes for C# verbatim string literals
void EscapeQuotes(char* dst, char const* src) {
    while (*src) {
        if (*src == '"')
            *dst++ = '"';

        *dst++ = *src++;
    }

    *dst = 0;
}

std::string GetEscaped(const char* s) {
    char escaped[0x200] = { 0 };
    EscapeQuotes(escaped, s);
    return std::string(escaped);
}

static constexpr const auto TestData_PathFormatterNormalize_EmptyPath = make_array(
    // Check AllowEmptyPath option
    std::make_tuple("", ""),
    std::make_tuple("", "E"),
    std::make_tuple("/aa/bb/../cc", "E")
);

static constexpr const auto TestData_PathFormatterNormalize_MountName = make_array(
    // Mount names should only be allowed with the AllowMountNames option
    std::make_tuple("mount:/aa/bb", ""), // Mount name isn't allowed without the AllowMountNames option
    std::make_tuple("mount:/aa/bb", "W"),
    std::make_tuple("mount:/aa/bb", "M"), // Basic mount names
    std::make_tuple("mount:/aa/./bb", "M"),
    std::make_tuple("mount:\\aa\\bb", "M"),
    std::make_tuple("m:/aa/bb", "M"), // Windows mount name without AllowWindowsPath option
    std::make_tuple("mo>unt:/aa/bb", "M"), // Mount names with invalid characters
    std::make_tuple("moun?t:/aa/bb", "M"),
    std::make_tuple("mo&unt:/aa/bb", "M"), // Mount name with valid special character
    std::make_tuple("/aa/./bb", "M"), // AllowMountName set when path has no mount name
    std::make_tuple("mount/aa/./bb", "M") // Relative path or mount name is missing separator
);

static constexpr const auto TestData_PathFormatterNormalize_WindowsPath = make_array(
    // Windows paths should only be allowed with the AllowWindowsPath option
    std::make_tuple(R"(c:/aa/bb)", ""),
    std::make_tuple(R"(c:\aa\bb)", ""),
    std::make_tuple(R"(\\host\share)", ""),
    std::make_tuple(R"(\\.\c:\)", ""),
    std::make_tuple(R"(\\.\c:/aa/bb/.)", ""),
    std::make_tuple(R"(\\?\c:\)", ""),
    std::make_tuple(R"(mount:\\host\share\aa\bb)", "M"), // Catch instances where the Windows path comes after other parts in the path
    std::make_tuple(R"(mount:\\host/share\aa\bb)", "M"), // And do it again with the UNC path not normalized

    std::make_tuple(R"(c:\aa\..\..\..\bb)", "W"), // Windows paths won't error when trying to navigate to the parent of the root directory
    std::make_tuple(R"(mount:/\\aa\..\bb)", "MW"),
    std::make_tuple(R"(mount:/c:\aa\..\bb)", "MW"),
    std::make_tuple(R"(mount:/aa/bb)", "MW"),
    std::make_tuple(R"(/mount:/aa/bb)", "MW"),
    std::make_tuple(R"(/mount:/aa/bb)", "W"),
    std::make_tuple(R"(a:aa/../bb)", "MW"),
    std::make_tuple(R"(a:aa\..\bb)", "MW"),
    std::make_tuple(R"(/a:aa\..\bb)", "W"),
    std::make_tuple(R"(\\?\c:\.\aa)", "W"), // Path with win32 file namespace prefix
    std::make_tuple(R"(\\.\c:\.\aa)", "W"), // Path with win32 device namespace prefix
    std::make_tuple(R"(\\.\mount:\.\aa)", "W"),
    std::make_tuple(R"(\\./.\aa)", "W"),
    std::make_tuple(R"(\\/aa)", "W"),
    std::make_tuple(R"(\\\aa)", "W"),
    std::make_tuple(R"(\\)", "W"),
    std::make_tuple(R"(\\host\share)", "W"), // Basic UNC paths
    std::make_tuple(R"(\\host\share\path)", "W"),
    std::make_tuple(R"(\\host\share\path\aa\bb\..\cc\.)", "W"), // UNC path using only backslashes that is not normalized
    std::make_tuple(R"(\\host\)", "W"), // Share name cannot be empty
    std::make_tuple(R"(\\ho$st\share\path)", "W"), // Invalid character '$' in host name
    std::make_tuple(R"(\\host:\share\path)", "W"), // Invalid character ':' in host name
    std::make_tuple(R"(\\..\share\path)", "W"), // Host name can't be ".."
    std::make_tuple(R"(\\host\s:hare\path)", "W"), // Invalid character ':' in host name
    std::make_tuple(R"(\\host\.\path)", "W"), // Share name can't be "."
    std::make_tuple(R"(\\host\..\path)", "W"), // Share name can't be ".."
    std::make_tuple(R"(\\host\sha:re)", "W"), // Invalid share name when nothing follows it
    std::make_tuple(R"(.\\host\share)", "RW") // Can't have a relative Windows path
);

static constexpr const auto TestData_PathFormatterNormalize_RelativePath = make_array(
    std::make_tuple("./aa/bb", ""), // Relative path isn't allowed without the AllowRelativePaths option
    std::make_tuple("./aa/bb/../cc", "R"), // Basic relative paths using different separators
    std::make_tuple(".\\aa/bb/../cc", "R"),
    std::make_tuple(".", "R"), // Standalone current directory
    std::make_tuple("../aa/bb", "R"), // Path starting with parent directory is not allowed
    std::make_tuple("/aa/./bb", "R"), // Absolute paths should work normally
    std::make_tuple("mount:./aa/bb", "MR"), // Mount name with relative path
    std::make_tuple("mount:./aa/./bb", "MR"),
    std::make_tuple("mount:./aa/bb", "M")
);

static constexpr const auto TestData_PathFormatterNormalize_Backslash = make_array(
    std::make_tuple(R"(\aa\bb\..\cc)", ""), // Paths can't start with a backslash no matter the path flags set
    std::make_tuple(R"(\aa\bb\..\cc)", "B"),
    std::make_tuple(R"(/aa\bb\..\cc)", ""), // Paths can contain backslashes if they start with a frontslash and have AllowBackslash set
    std::make_tuple(R"(/aa\bb\..\cc)", "B"), // When backslashes are allowed they do not count as a directory separator
    std::make_tuple(R"(/aa\bb\cc)", ""), // Normalized path without a prefix except it uses backslashes
    std::make_tuple(R"(/aa\bb\cc)", "B"),
    std::make_tuple(R"(\\host\share\path\aa\bb\cc)", "W"), // Otherwise normalized Windows path except with backslashes
    std::make_tuple(R"(\\host\share\path\aa\bb\cc)", "WB"),
    std::make_tuple(R"(/aa/bb\../cc/..\dd\..\ee/..)", ""), // Path with "parent directory path replacement needed"
    std::make_tuple(R"(/aa/bb\../cc/..\dd\..\ee/..)", "B")
);

static constexpr const auto TestData_PathFormatterNormalize_AllowAllChars = make_array(
    std::make_tuple(R"(/aa/b:b/cc)", ""), // Test each of the characters that normally aren't allowed
    std::make_tuple(R"(/aa/b*b/cc)", ""),
    std::make_tuple(R"(/aa/b?b/cc)", ""),
    std::make_tuple(R"(/aa/b<b/cc)", ""),
    std::make_tuple(R"(/aa/b>b/cc)", ""),
    std::make_tuple(R"(/aa/b|b/cc)", ""),
    std::make_tuple(R"(/aa/b:b/cc)", "C"),
    std::make_tuple(R"(/aa/b*b/cc)", "C"),
    std::make_tuple(R"(/aa/b?b/cc)", "C"),
    std::make_tuple(R"(/aa/b<b/cc)", "C"),
    std::make_tuple(R"(/aa/b>b/cc)", "C"),
    std::make_tuple(R"(/aa/b|b/cc)", "C"),

    std::make_tuple(R"(/aa/b'b/cc)", ""), // Test some symbols that are normally allowed
    std::make_tuple(R"(/aa/b"b/cc)", ""),
    std::make_tuple(R"(/aa/b(b/cc)", ""),
    std::make_tuple(R"(/aa/b)b/cc)", ""),
    std::make_tuple(R"(/aa/b'b/cc)", "C"),
    std::make_tuple(R"(/aa/b"b/cc)", "C"),
    std::make_tuple(R"(/aa/b(b/cc)", "C"),
    std::make_tuple(R"(/aa/b)b/cc)", "C"),

    std::make_tuple(R"(mount:/aa/b<b/cc)", "MC"),
    std::make_tuple(R"(mo>unt:/aa/bb/cc)", "MC") // Invalid character in mount name
);

static constexpr const auto TestData_PathFormatterNormalize_All = make_array(
    std::make_tuple(R"(mount:./aa/bb)", "WRM"), // Normalized path with both mount name and relative path
    std::make_tuple(R"(mount:./aa/bb\cc/dd)", "WRM"), // Path with backslashes
    std::make_tuple(R"(mount:./aa/bb\cc/dd)", "WRMB"), // This path is considered normalized but the backslashes still normalize to forward slashes
    std::make_tuple(R"(mount:./.c:/aa/bb)", "RM"), // These next 2 form a chain where if you normalize one it'll turn into the next
    std::make_tuple(R"(mount:.c:/aa/bb)", "WRM"),
    std::make_tuple(R"(mount:./cc:/aa/bb)", "WRM"),
    std::make_tuple(R"(mount:./\\host\share/aa/bb)", "MW"),
    std::make_tuple(R"(mount:./\\host\share/aa/bb)", "WRM"), // These next 3 form a chain where if you normalize one it'll turn into the next
    std::make_tuple(R"(mount:.\\host\share/aa/bb)", "WRM"),
    std::make_tuple(R"(mount:..\\host\share/aa/bb)", "WRM"),
    std::make_tuple(R"(.\\host\share/aa/bb)", "WRM"), // These next 2 form a chain where if you normalize one it'll turn into the next
    std::make_tuple(R"(..\\host\share/aa/bb)", "WRM"),
    std::make_tuple(R"(mount:\\host\share/aa/bb)", "MW"), // Use a mount name and windows path together
    std::make_tuple(R"(mount:\aa\bb)", "BM"), // Backslashes are never allowed directly after a mount name even with AllowBackslashes
    std::make_tuple(R"(mount:/aa\bb)", "BM"),
    std::make_tuple(R"(.//aa/bb)", "RW"), // Relative path followed by a Windows path won't work
    std::make_tuple(R"(./aa/bb)", "R"),
    std::make_tuple(R"(./c:/aa/bb)", "RW"),
    std::make_tuple(R"(mount:./aa/b:b\cc/dd)", "WRMBC") // This path is considered normalized but the backslashes still normalize to forward slashes
);

void CreateTest_PathFormatterNormalize(char const* path, char const* pathFlags) {
    char normalized[0x200] = { 0 };
    nn::fs::PathFlags flags = GetPathFlags(pathFlags);

    nn::Result result = nn::fs::PathFormatter::Normalize(normalized, 0x200, path, 0x200, flags);

    BufPos += sprintf(&Buf[BufPos], "{@\"%s\", \"%s\", @\"%s\", %s},\n",
        GetEscaped(path).c_str(), pathFlags, GetEscaped(normalized).c_str(), GetResultName(result));
}

void CreateTest_PathFormatterIsNormalized(char const* path, char const* pathFlags) {
    bool isNormalized = 0;
    uint64_t normalizedLength = 0;
    nn::fs::PathFlags flags = GetPathFlags(pathFlags);

    nn::Result result = nn::fs::PathFormatter::IsNormalized(&isNormalized, &normalizedLength, path, flags);

    BufPos += sprintf(&Buf[BufPos], "{@\"%s\", \"%s\", %s, %ld, %s},\n",
        GetEscaped(path).c_str(), pathFlags, BoolStr(isNormalized), normalizedLength, GetResultName(result));
}

static constexpr const auto TestData_PathFormatterNormalize_SmallBuffer = make_array(
    //std::make_tuple(R"(aa/bb)", "MR", 2), // Crashes nnsdk and throws an out-of-range exception in LibHac. I guess that counts as a pass?
    std::make_tuple(R"(/aa/bb)", "M", 1),
    std::make_tuple(R"(mount:/aa/bb)", "MR", 6),
    std::make_tuple(R"(mount:/aa/bb)", "MR", 7),
    std::make_tuple(R"(aa/bb)", "MR", 3),
    std::make_tuple(R"(\\host\share)", "W", 13)
);

void CreateTest_PathFormatterNormalize_SmallBuffer(char const* path, char const* pathFlags, int bufferSize) {
    char normalized[0x200] = { 0 };
    nn::fs::PathFlags flags = GetPathFlags(pathFlags);

    nn::Result result = nn::fs::PathFormatter::Normalize(normalized, bufferSize, path, 0x200, flags);

    BufPos += sprintf(&Buf[BufPos], "{@\"%s\", \"%s\", %d, @\"%s\", %s},\n",
        GetEscaped(path).c_str(), pathFlags, bufferSize, GetEscaped(normalized).c_str(), GetResultName(result));
}

static constexpr const auto TestData_PathNormalizerNormalize = make_array(
    std::make_tuple("/aa/bb/c/", false, true, false),
    std::make_tuple("aa/bb/c/", false, false, false),
    std::make_tuple("aa/bb/c/", false, true, false),
    std::make_tuple("mount:a/b", false, true, false),
    std::make_tuple("mo|unt:a/b", false, true, true),
    std::make_tuple("/aa/bb/../..", true, false, false), // Windows paths won't error when trying to navigate to the parent of the root directory
    std::make_tuple("/aa/bb/../../..", true, false, false),
    std::make_tuple("/aa/bb/../../..", false, false, false),
    std::make_tuple("aa/bb/../../..", true, true, false),
    std::make_tuple("aa/bb/../../..", false, true, false),
    std::make_tuple("mount:a/b", false, true, true), // Test allowing invalid characters
    std::make_tuple("/a|/bb/cc", false, false, true),
    std::make_tuple("/>a/bb/cc", false, false, true),
    std::make_tuple("/aa/.</cc", false, false, true),
    std::make_tuple("/aa/..</cc", false, false, true),
    std::make_tuple("", false, false, false),
    std::make_tuple("/", false, false, false),
    std::make_tuple("/.", false, false, false),
    std::make_tuple("/./", false, false, false),
    std::make_tuple("/..", false, false, false),
    std::make_tuple("//.", false, false, false),
    std::make_tuple("/ ..", false, false, false),
    std::make_tuple("/.. /", false, false, false),
    std::make_tuple("/. /.", false, false, false),
    std::make_tuple("/aa/bb/cc/dd/./.././../..", false, false, false),
    std::make_tuple("/aa/bb/cc/dd/./.././../../..", false, false, false),
    std::make_tuple("/./aa/./bb/./cc/./dd/.", false, false, false),
    std::make_tuple("/aa\\bb/cc", false, false, false),
    std::make_tuple("/aa\\bb/cc", false, false, false),
    std::make_tuple("/a|/bb/cc", false, false, false),
    std::make_tuple("/>a/bb/cc", false, false, false),
    std::make_tuple("/aa/.</cc", false, false, false),
    std::make_tuple("/aa/..</cc", false, false, false),
    std::make_tuple("\\\\aa/bb/cc", false, false, false),
    std::make_tuple("\\\\aa\\bb\\cc", false, false, false),
    std::make_tuple("/aa/bb/..\\cc", false, false, false),
    std::make_tuple("/aa/bb\\..\\cc", false, false, false),
    std::make_tuple("/aa/bb\\..", false, false, false),
    std::make_tuple("/aa\\bb/../cc", false, false, false)
);

void CreateTest_PathNormalizerNormalize(char const* path, bool isWindowsPath, bool isRelativePath, bool allowAllCharacters) {
    char normalized[0x200] = { 0 };
    uint64_t normalizedLength = 0;

    nn::Result result = nn::fs::PathNormalizer::Normalize(normalized, &normalizedLength, path, 0x200, isWindowsPath, isRelativePath, allowAllCharacters);

    BufPos += sprintf(&Buf[BufPos], "{@\"%s\", %s, %s, %s, @\"%s\", %ld, %s},\n",
        GetEscaped(path).c_str(), BoolStr(isWindowsPath), BoolStr(isRelativePath), BoolStr(allowAllCharacters), GetEscaped(normalized).c_str(), normalizedLength, GetResultName(result));
}

void CreateTest_PathNormalizerIsNormalized(char const* path, bool isWindowsPath, bool isRelativePath, bool allowAllCharacters) {
    bool isNormalized = false;
    uint64_t normalizedLength = 0;

    nn::Result result = nn::fs::PathNormalizer::IsNormalized(&isNormalized, &normalizedLength, path, allowAllCharacters);

    BufPos += sprintf(&Buf[BufPos], "{@\"%s\", %s, %s, %ld, %s},\n",
        GetEscaped(path).c_str(), BoolStr(allowAllCharacters), BoolStr(isNormalized), normalizedLength, GetResultName(result));
}

static constexpr const auto TestData_PathNormalizerNormalize_SmallBuffer = make_array(
    std::make_tuple("/aa/bb/cc/", 7),
    std::make_tuple("/aa/bb/cc/", 8),
    std::make_tuple("/aa/bb/cc/", 9),
    std::make_tuple("/aa/bb/cc/", 10),
    std::make_tuple("/aa/bb/cc", 9),
    std::make_tuple("/aa/bb/cc", 10),
    std::make_tuple("/./aa/./bb/./cc", 9),
    std::make_tuple("/./aa/./bb/./cc", 10),
    std::make_tuple("/aa/bb/cc/../../..", 9),
    std::make_tuple("/aa/bb/cc/../../..", 10),
    std::make_tuple("/aa/bb/.", 7),
    std::make_tuple("/aa/bb/./", 7),
    std::make_tuple("/aa/bb/..", 8),
    std::make_tuple("/aa/bb", 1),
    std::make_tuple("/aa/bb", 2),
    std::make_tuple("/aa/bb", 3),
    std::make_tuple("aa/bb", 1)
);

void CreateTest_PathNormalizerNormalize_SmallBuffer(char const* path, int bufferSize) {
    char normalized[0x200] = { 0 };
    uint64_t normalizedLength = 0;

    nn::Result result = nn::fs::PathNormalizer::Normalize(normalized, &normalizedLength, path, bufferSize, false, false);

    BufPos += sprintf(&Buf[BufPos], "{@\"%s\", %d, @\"%s\", %ld, %s},\n",
        GetEscaped(path).c_str(), bufferSize, GetEscaped(normalized).c_str(), normalizedLength, GetResultName(result));
}

static constexpr const auto TestData_PathUtility_IsSubPath = make_array(
    std::make_tuple("//a/b", "/a"),
    std::make_tuple("/a", "//a/b"),
    std::make_tuple("//a/b", "\\\\a"),
    std::make_tuple("//a/b", "//a"),
    std::make_tuple("/", "/a"),
    std::make_tuple("/a", "/"),
    std::make_tuple("/", "/"),
    std::make_tuple("", ""),
    std::make_tuple("/", ""),
    std::make_tuple("/", "mount:/a"),
    std::make_tuple("mount:/", "mount:/"),
    std::make_tuple("mount:/a/b", "mount:/a/b"),
    std::make_tuple("mount:/a/b", "mount:/a/b/c"),
    std::make_tuple("/a/b", "/a/b/c"),
    std::make_tuple("/a/b/c", "/a/b"),
    std::make_tuple("/a/b", "/a/b"),
    std::make_tuple("/a/b", "/a/b\\c")
);

void CreateTest_PathUtility_IsSubPath(const char* path1, const char* path2) {
    bool result = nn::fs::IsSubPath(path1, path2);

    BufPos += sprintf(&Buf[BufPos], "{@\"%s\", @\"%s\", %s},\n",
        GetEscaped(path1).c_str(), GetEscaped(path2).c_str(), BoolStr(result));
}

void RunTest_Path_RemoveChild() {
    auto path = nn::fs::Path();
    nn::Result result = path.InitializeWithReplaceUnc("/aa/bb/./cc");
    BufPos += sprintf(&Buf[BufPos], "%s\n", GetResultName(result));
    BufPos += sprintf(&Buf[BufPos], "%s\n", path.m_String);

    result = path.InitializeWithReplaceUnc("//aa/bb");
    BufPos += sprintf(&Buf[BufPos], "%s\n", GetResultName(result));
    BufPos += sprintf(&Buf[BufPos], "%s\n", path.m_String);

    result = path.InitializeWithReplaceUnc("@Host://aa/bb");
    BufPos += sprintf(&Buf[BufPos], "%s\n", GetResultName(result));
    BufPos += sprintf(&Buf[BufPos], "%s\n", path.m_String);

    result = path.InitializeWithReplaceUnc("mount:///aa/bb");
    BufPos += sprintf(&Buf[BufPos], "%s\n", GetResultName(result));
    BufPos += sprintf(&Buf[BufPos], "%s\n", path.m_String);

    result = path.InitializeWithReplaceUnc("//mount:///aa/bb");
    BufPos += sprintf(&Buf[BufPos], "%s\n", GetResultName(result));
    BufPos += sprintf(&Buf[BufPos], "%s\n", path.m_String);

    svcOutputDebugString(Buf, BufPos);
};

void RunTest_Path_InsertParent() {
    auto path = nn::fs::Path();
    nn::Result result1 = path.Initialize("/cc/dd");
    nn::Result result2 = path.InsertParent("/aa/bb");
    BufPos += sprintf(&Buf[BufPos], "%s\n%s\n%s\n", GetResultName(result1), GetResultName(result2), path.m_String);

    result1 = path.Initialize("/cc/dd");
    result2 = path.InsertParent("aa/bb");
    BufPos += sprintf(&Buf[BufPos], "%s\n%s\n%s\n", GetResultName(result1), GetResultName(result2), path.m_String);

    result1 = path.Initialize("/cc/dd/");
    result2 = path.InsertParent("aa/bb");
    BufPos += sprintf(&Buf[BufPos], "%s\n%s\n%s\n", GetResultName(result1), GetResultName(result2), path.m_String);

    result1 = path.Initialize("/cc/dd/");
    result2 = path.InsertParent("/aa/bb");
    BufPos += sprintf(&Buf[BufPos], "%s\n%s\n%s\n", GetResultName(result1), GetResultName(result2), path.m_String);

    result1 = path.Initialize("/cc/dd/");
    result2 = path.Normalize(GetPathFlags(""));
    BufPos += sprintf(&Buf[BufPos], "%s\n%s\n%s\n", GetResultName(result1), GetResultName(result2), BoolStr(path.m_IsNormalized));

    result2 = path.InsertParent("/aa/../bb");
    BufPos += sprintf(&Buf[BufPos], "%s\n%s\n%s\n", GetResultName(result2), BoolStr(path.m_IsNormalized), path.m_String);

    svcOutputDebugString(Buf, BufPos);
};

extern "C" void nnMain(void) {
    // nn::fs::detail::IsEnabledAccessLog(); // Adds the sdk version to the output

    CreateTest("TestData_PathFormatter_Normalize_EmptyPath", CreateTest_PathFormatterNormalize, TestData_PathFormatterNormalize_EmptyPath);
    CreateTest("TestData_PathFormatter_Normalize_MountName", CreateTest_PathFormatterNormalize, TestData_PathFormatterNormalize_MountName);
    CreateTest("TestData_PathFormatter_Normalize_WindowsPath", CreateTest_PathFormatterNormalize, TestData_PathFormatterNormalize_WindowsPath);
    CreateTest("TestData_PathFormatter_Normalize_RelativePath", CreateTest_PathFormatterNormalize, TestData_PathFormatterNormalize_RelativePath);
    CreateTest("TestData_PathFormatter_Normalize_Backslash", CreateTest_PathFormatterNormalize, TestData_PathFormatterNormalize_Backslash);
    CreateTest("TestData_PathFormatter_Normalize_AllowAllChars", CreateTest_PathFormatterNormalize, TestData_PathFormatterNormalize_AllowAllChars);
    CreateTest("TestData_PathFormatter_Normalize_All", CreateTest_PathFormatterNormalize, TestData_PathFormatterNormalize_All);
    CreateTest("TestData_PathFormatter_Normalize_SmallBuffer", CreateTest_PathFormatterNormalize_SmallBuffer, TestData_PathFormatterNormalize_SmallBuffer);

    CreateTest("TestData_PathFormatter_IsNormalized_EmptyPath", CreateTest_PathFormatterIsNormalized, TestData_PathFormatterNormalize_EmptyPath);
    CreateTest("TestData_PathFormatter_IsNormalized_MountName", CreateTest_PathFormatterIsNormalized, TestData_PathFormatterNormalize_MountName);
    CreateTest("TestData_PathFormatter_IsNormalized_WindowsPath", CreateTest_PathFormatterIsNormalized, TestData_PathFormatterNormalize_WindowsPath);
    CreateTest("TestData_PathFormatter_IsNormalized_RelativePath", CreateTest_PathFormatterIsNormalized, TestData_PathFormatterNormalize_RelativePath);
    CreateTest("TestData_PathFormatter_IsNormalized_Backslash", CreateTest_PathFormatterIsNormalized, TestData_PathFormatterNormalize_Backslash);
    CreateTest("TestData_PathFormatter_IsNormalized_AllowAllChars", CreateTest_PathFormatterIsNormalized, TestData_PathFormatterNormalize_AllowAllChars);
    CreateTest("TestData_PathFormatter_IsNormalized_All", CreateTest_PathFormatterIsNormalized, TestData_PathFormatterNormalize_All);

    CreateTest("TestData_PathNormalizer_Normalize", CreateTest_PathNormalizerNormalize, TestData_PathNormalizerNormalize);
    CreateTest("TestData_PathNormalizer_Normalize_SmallBuffer", CreateTest_PathNormalizerNormalize_SmallBuffer, TestData_PathNormalizerNormalize_SmallBuffer);
    CreateTest("TestData_PathNormalizer_IsNormalized", CreateTest_PathNormalizerIsNormalized, TestData_PathNormalizerNormalize);

    CreateTest("TestData_PathUtility_IsSubPath", CreateTest_PathUtility_IsSubPath, TestData_PathUtility_IsSubPath);
}
