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

// SDK 12
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

        const bool IsWindowsPathAllowed() { return (value & (1 << 0)) != 0; }
        const bool IsRelativePathAllowed() { return (value & (1 << 1)) != 0; }
        const bool IsEmptyPathAllowed() { return (value & (1 << 2)) != 0; }
        const bool IsMountNameAllowed() { return (value & (1 << 3)) != 0; }
        const bool IsBackslashAllowed() { return (value & (1 << 4)) != 0; }
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
        static nn::Result IsNormalized(bool* outIsNormalized, uint64_t* outNormalizedPathLength, const char* path);
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
        }
    }

    return flags;
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
    std::make_tuple(R"(./c:/aa/bb)", "RW")
);

void CreateTest_PathFormatterNormalize(char const* path, char const* pathFlags) {
    char normalized[0x200] = { 0 };
    nn::fs::PathFlags flags = GetPathFlags(pathFlags);

    nn::Result result = nn::fs::PathFormatter::Normalize(normalized, 0x200, path, 0x200, flags);

    BufPos += sprintf(&Buf[BufPos], "{@\"%s\", \"%s\", @\"%s\", %s},\n",
        path, pathFlags, normalized, GetResultName(result));
}

void CreateTest_PathFormatterIsNormalized(char const* path, char const* pathFlags) {
    bool isNormalized = 0;
    uint64_t normalizedLength = 0;
    nn::fs::PathFlags flags = GetPathFlags(pathFlags);

    nn::Result result = nn::fs::PathFormatter::IsNormalized(&isNormalized, &normalizedLength, path, flags);

    BufPos += sprintf(&Buf[BufPos], "{@\"%s\", \"%s\", %s, %ld, %s},\n",
        path, pathFlags, BoolStr(isNormalized), normalizedLength, GetResultName(result));
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

    svcOutputDebugString(path, strnlen(path, 0x200));

    nn::Result result = nn::fs::PathFormatter::Normalize(normalized, bufferSize, path, 0x200, flags);

    BufPos += sprintf(&Buf[BufPos], "{@\"%s\", \"%s\", %d, @\"%s\", %s},\n",
        path, pathFlags, bufferSize, normalized, GetResultName(result));
}

static constexpr const auto TestData_PathNormalizerNormalize = make_array(
    std::make_tuple("/aa/bb/c/", false, true),
    std::make_tuple("aa/bb/c/", false, false),
    std::make_tuple("aa/bb/c/", false, true),
    std::make_tuple("mount:a/b", false, true),
    std::make_tuple("/aa/bb/../..", true, false),
    std::make_tuple("/aa/bb/../../..", true, false),
    std::make_tuple("/aa/bb/../../..", false, false),
    std::make_tuple("aa/bb/../../..", true, true),
    std::make_tuple("aa/bb/../../..", false, true),
    std::make_tuple("", false, false),
    std::make_tuple("/", false, false),
    std::make_tuple("/.", false, false),
    std::make_tuple("/./", false, false),
    std::make_tuple("/..", false, false),
    std::make_tuple("//.", false, false),
    std::make_tuple("/ ..", false, false),
    std::make_tuple("/.. /", false, false),
    std::make_tuple("/. /.", false, false),
    std::make_tuple("/aa/bb/cc/dd/./.././../..", false, false),
    std::make_tuple("/aa/bb/cc/dd/./.././../../..", false, false),
    std::make_tuple("/./aa/./bb/./cc/./dd/.", false, false),
    std::make_tuple("/aa\\bb/cc", false, false),
    std::make_tuple("/aa\\bb/cc", false, false),
    std::make_tuple("/a|/bb/cc", false, false),
    std::make_tuple("/>a/bb/cc", false, false),
    std::make_tuple("/aa/.</cc", false, false),
    std::make_tuple("/aa/..</cc", false, false),
    std::make_tuple("\\\\aa/bb/cc", false, false),
    std::make_tuple("\\\\aa\\bb\\cc", false, false),
    std::make_tuple("/aa/bb/..\\cc", false, false),
    std::make_tuple("/aa/bb\\..\\cc", false, false),
    std::make_tuple("/aa/bb\\..", false, false),
    std::make_tuple("/aa\\bb/../cc", false, false)
);

void CreateTest_PathNormalizerNormalize(char const* path, bool isWindowsPath, bool isRelativePath) {
    char normalized[0x200] = { 0 };
    uint64_t normalizedLength = 0;

    nn::Result result = nn::fs::PathNormalizer::Normalize(normalized, &normalizedLength, path, 0x200, isWindowsPath, isRelativePath);

    BufPos += sprintf(&Buf[BufPos], "{@\"%s\", %s, %s, @\"%s\", %ld, %s},\n",
        path, BoolStr(isWindowsPath), BoolStr(isRelativePath), normalized, normalizedLength, GetResultName(result));
}

void CreateTest_PathNormalizerIsNormalized(char const* path, bool isWindowsPath, bool isRelativePath) {
    bool isNormalized = false;
    uint64_t normalizedLength = 0;

    nn::Result result = nn::fs::PathNormalizer::IsNormalized(&isNormalized, &normalizedLength, path);

    BufPos += sprintf(&Buf[BufPos], "{@\"%s\", %s, %ld, %s},\n",
        path, BoolStr(isNormalized), normalizedLength, GetResultName(result));
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
        path, bufferSize, normalized, normalizedLength, GetResultName(result));
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
        path1, path2, BoolStr(result));
}

extern "C" void nnMain(void) {
    // nn::fs::detail::IsEnabledAccessLog(); // Adds the sdk version to the output

    CreateTest("TestData_PathFormatter_Normalize_EmptyPath", CreateTest_PathFormatterNormalize, TestData_PathFormatterNormalize_EmptyPath);
    CreateTest("TestData_PathFormatter_Normalize_MountName", CreateTest_PathFormatterNormalize, TestData_PathFormatterNormalize_MountName);
    CreateTest("TestData_PathFormatter_Normalize_WindowsPath", CreateTest_PathFormatterNormalize, TestData_PathFormatterNormalize_WindowsPath);
    CreateTest("TestData_PathFormatter_Normalize_RelativePath", CreateTest_PathFormatterNormalize, TestData_PathFormatterNormalize_RelativePath);
    CreateTest("TestData_PathFormatter_Normalize_Backslash", CreateTest_PathFormatterNormalize, TestData_PathFormatterNormalize_Backslash);
    CreateTest("TestData_PathFormatter_Normalize_All", CreateTest_PathFormatterNormalize, TestData_PathFormatterNormalize_All);
    CreateTest("TestData_PathFormatter_Normalize_SmallBuffer", CreateTest_PathFormatterNormalize_SmallBuffer, TestData_PathFormatterNormalize_SmallBuffer);

    CreateTest("TestData_PathFormatter_IsNormalized_EmptyPath", CreateTest_PathFormatterIsNormalized, TestData_PathFormatterNormalize_EmptyPath);
    CreateTest("TestData_PathFormatter_IsNormalized_MountName", CreateTest_PathFormatterIsNormalized, TestData_PathFormatterNormalize_MountName);
    CreateTest("TestData_PathFormatter_IsNormalized_WindowsPath", CreateTest_PathFormatterIsNormalized, TestData_PathFormatterNormalize_WindowsPath);
    CreateTest("TestData_PathFormatter_IsNormalized_RelativePath", CreateTest_PathFormatterIsNormalized, TestData_PathFormatterNormalize_RelativePath);
    CreateTest("TestData_PathFormatter_IsNormalized_Backslash", CreateTest_PathFormatterIsNormalized, TestData_PathFormatterNormalize_Backslash);
    CreateTest("TestData_PathFormatter_IsNormalized_All", CreateTest_PathFormatterIsNormalized, TestData_PathFormatterNormalize_All);

    CreateTest("TestData_PathNormalizer_Normalize", CreateTest_PathNormalizerNormalize, TestData_PathNormalizerNormalize);
    CreateTest("TestData_PathNormalizer_Normalize_SmallBuffer", CreateTest_PathNormalizerNormalize_SmallBuffer, TestData_PathNormalizerNormalize_SmallBuffer);
    CreateTest("TestData_PathNormalizer_IsNormalized", CreateTest_PathNormalizerIsNormalized, TestData_PathNormalizerNormalize);

    CreateTest("TestData_PathUtility_IsSubPath", CreateTest_PathUtility_IsSubPath, TestData_PathUtility_IsSubPath);
}
