// Uses GLoat to run code in nnsdk https://github.com/h1k421/GLoat
#include <gloat.hpp>

static char Buf[0x80000];
static int BufPos = 0;

static char ResultNameBuf[0x100];

namespace nn::fs::detail {
	bool IsEnabledAccessLog();
}

namespace nn::fs::PathTool {
	// SDK < 7
	nn::Result Normalize(char* buffer, uint64_t* outNormalizedPathLength, const char* path, uint64_t bufferLength, bool preserveUnc);
	nn::Result IsNormalized(bool* outIsNormalized, const char* path);

	// SDK >= 7
	nn::Result Normalize(char* buffer, uint64_t* outNormalizedPathLength, const char* path, uint64_t bufferLength, bool preserveUnc, bool hasMountName);
	nn::Result IsNormalized(bool* outIsNormalized, const char* path, bool preserveUnc, bool hasMountName);
}

void* allocate(size_t size)
{
	void* ptr = malloc(size);

	char buffer[0x40];
	sprintf(buffer, "Allocating %ld. 0x%p", size, ptr);
	svcOutputDebugString(buffer, sizeof(buffer));

	return ptr;
}

void deallocate(void* ptr, size_t size)
{
	char buffer[0x40];
	sprintf(buffer, "Deallocating %ld. 0x%p", size, ptr);
	svcOutputDebugString(buffer, sizeof(buffer));

	free(ptr);
}

void setAllocators(void) {
	nn::fs::SetAllocator(allocate, deallocate);
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

void CreateNormalizeTestData(char const* path, bool preserveUnc, bool hasMountName) {
	char normalized[0x200];
	uint64_t normalizedLen = 0;
	memset(normalized, 0, 0x200);

	//svcOutputDebugString(path, strnlen(path, 0x200));

	nn::Result result = nn::fs::PathTool::Normalize(normalized, &normalizedLen, path, 0x200, preserveUnc, hasMountName);

	const char* preserveUncStr = preserveUnc ? "true" : "false";
	const char* hasMountNameStr = hasMountName ? "true" : "false";
	BufPos += sprintf(&Buf[BufPos], "new object[] {@\"%s\", %s, %s, @\"%s\", %ld, %s},\n",
		path, preserveUncStr, hasMountNameStr, normalized, normalizedLen, GetResultName(result));
}

void CreateIsNormalizedTestData(char const* path, bool preserveUnc, bool hasMountName) {
	bool isNormalized = false;

	nn::Result result = nn::fs::PathTool::IsNormalized(&isNormalized, path, preserveUnc, hasMountName);

	const char* preserveUncStr = preserveUnc ? "true" : "false";
	const char* hasMountNameStr = hasMountName ? "true" : "false";
	const char* isNormalizedStr = isNormalized ? "true" : "false";
	BufPos += sprintf(&Buf[BufPos], "new object[] {@\"%s\", %s, %s, %s, %s},\n",
		path, preserveUncStr, hasMountNameStr, isNormalizedStr, GetResultName(result));
}

void CreateTestDataWithParentDirs(char const* path, bool preserveUnc, bool hasMountName, void (*func)(char const*, bool, bool), int parentCount) {
	char parentPath[0x200];
	memset(parentPath, 0, sizeof(parentPath));

	strcpy(parentPath, path);
	func(parentPath, preserveUnc, hasMountName);

	for (int i = 0; i < parentCount; i++) {
		strcat(parentPath, "/..");
		func(parentPath, preserveUnc, hasMountName);
	}
}

void CreateTestDataWithParentDirs(char const* path, bool preserveUnc, bool hasMountName, void (*func)(char const*, bool, bool)) {
	CreateTestDataWithParentDirs(path, preserveUnc, hasMountName, func, 3);
}

void CreateTestData(void (*func)(char const*, bool, bool)) {
	Buf[0] = '\n';
	BufPos = 1;

	bool preserveUnc = false;

	func("", preserveUnc, false);
	func("/", preserveUnc, false);
	func("/.", preserveUnc, false);
	func("/a/b/c", preserveUnc, false);
	func("/a/b/../c", preserveUnc, false);
	func("/a/b/c/..", preserveUnc, false);
	func("/a/b/c/.", preserveUnc, false);
	func("/a/../../..", preserveUnc, false);
	func("/a/../../../a/b/c", preserveUnc, false);
	func("//a/b//.//c", preserveUnc, false);
	func("/../a/b/c/.", preserveUnc, false);
	func("/./aaa/bbb/ccc/.", preserveUnc, false);
	func("/a/b/c/", preserveUnc, false);
	func("a/b/c/", preserveUnc, false);
	func("/aa/./bb/../cc/", preserveUnc, false);
	func("/./b/../c/", preserveUnc, false);
	func("/a/../../../", preserveUnc, false);
	func("//a/b//.//c/", preserveUnc, false);
	func("/tmp/../", preserveUnc, false);
	func("a", preserveUnc, false);
	func("a/../../../a/b/c", preserveUnc, false);
	func("./b/../c/", preserveUnc, false);
	func(".", preserveUnc, false);
	func("..", preserveUnc, false);
	func("../a/b/c/.", preserveUnc, false);
	func("./a/b/c/.", preserveUnc, false);
	func("abc", preserveUnc, false);
	func("mount:/a/b/../c", preserveUnc, true);
	func("a:/a/b/c", preserveUnc, true);
	func("mount:/a/b/../c", preserveUnc, true);
	func("mount:\\a/b/../c", preserveUnc, true);
	func("mount:\\a/b\\../c", preserveUnc, true);
	func("mount:\\a/b/c", preserveUnc, true);
	func("mount:/a\\../b\\..c", preserveUnc, true);
	func("mount:/a\\../b/..cd", preserveUnc, true);
	func("mount:/a\\..d/b/c\\..", preserveUnc, true);
	func("mount:", preserveUnc, true);
	func("abc:/a/../../../a/b/c", preserveUnc, true);
	func("abc:/./b/../c/", preserveUnc, true);
	func("abc:/.", preserveUnc, true);
	func("abc:/..", preserveUnc, true);
	func("abc:/", preserveUnc, true);
	func("abc://a/b//.//c", preserveUnc, true);
	func("abc:/././/././a/b//.//c", preserveUnc, true);
	func("mount:/d./aa", preserveUnc, true);
	func("mount:/d/..", preserveUnc, true);
	func("/path/aaa/bbb\\..\\h/ddd", preserveUnc, false);
	func("/path/aaa/bbb/../h/ddd", preserveUnc, false);
	func("/path/aaa/bbb\\.\\h/ddd", preserveUnc, false);
	func("/path/aaa/bbb\\./h/ddd", preserveUnc, false);
	func("/path/aaa/bbb/./h/ddd", preserveUnc, false);
	func("mount:abcd", preserveUnc, true);
	func("mount:", preserveUnc, true);
	func("mount:/", preserveUnc, true);
	func("mount:\\..", preserveUnc, true);
	func("mount:/a/b\\..", preserveUnc, true);
	func("mount:/dir", preserveUnc, true);
	func("mount:/dir/", preserveUnc, true);
	func("mount:\\", preserveUnc, true);
	func("mo.unt:\\", preserveUnc, true);
	func("mount.:\\", preserveUnc, true);
	func("mount:./aa/bb", preserveUnc, true);
	//func("mount:../aa/bb", preserveUnc, true); // crashes nnsdk
	func("mount:.../aa/bb", preserveUnc, true);
	func("mount:...aa/bb", preserveUnc, true);
	func("...aa/bb", preserveUnc, false);
	func("mount01234567890/aa/bb", preserveUnc, true);
	func("mount01234567890:/aa/bb", preserveUnc, true);
	func("mount0123456789:/aa/bb", preserveUnc, true);
	func("mount012345678:/aa/bb", preserveUnc, true);
	func("mount:aa/..\\bb", preserveUnc, true);
	func("mount:..\\bb", preserveUnc, true);
	func("mount:/..\\bb", preserveUnc, true);
	func("mount:/.\\bb", preserveUnc, true);
	func("mount:\\..\\bb", preserveUnc, true);
	func("mount:\\.\\bb", preserveUnc, true);
	func("mount:/a\\..\\bb", preserveUnc, true);
	func("mount:/a\\.\\bb", preserveUnc, true);

	for (int i = 0; i < 2; i++) {
		preserveUnc = (bool)i;

		CreateTestDataWithParentDirs("//$abc/bb", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("//:abc/bb", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("\\\\\\asd", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("\\\\/asd", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("\\\\//asd", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("//", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("\\\\a/b/cc/../d", preserveUnc, false, func);
		CreateTestDataWithParentDirs("c:/aa/bb", preserveUnc, true, func);
		CreateTestDataWithParentDirs("mount:\\c:/aa", preserveUnc, true, func);
		CreateTestDataWithParentDirs("mount:/c:\\aa/bb", preserveUnc, true, func);
		CreateTestDataWithParentDirs("mount:////c:\\aa/bb", preserveUnc, true, func);
		CreateTestDataWithParentDirs("mount:/\\aa/bb", preserveUnc, true, func);
		CreateTestDataWithParentDirs("mount:/c:/aa/bb", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("mount:c:/aa/bb", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("mount:c:/aa/bb", preserveUnc, true, func, 0);
		CreateTestDataWithParentDirs("mount:/\\aa/../b", preserveUnc, true, func, 2);
		CreateTestDataWithParentDirs("mount://aa/bb", preserveUnc, true, func, 1);
		CreateTestDataWithParentDirs("//aa/bb", preserveUnc, true, func, 1);
		CreateTestDataWithParentDirs("//aa/bb", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("/aa/bb", preserveUnc, false, func);
		CreateTestDataWithParentDirs("c:/aa", preserveUnc, false, func, 2);
		CreateTestDataWithParentDirs("c:abcde/aa/bb", preserveUnc, false, func);
		CreateTestDataWithParentDirs("c:abcde", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("c:abcde/", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("///aa", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("//aa//bb", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("//./bb", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("//../bb", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("//.../bb", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("//aa$abc/bb", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("//aa$/bb", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("//aa:/bb", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("//aa/bb$b/cc$", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("//aa/bb/cc$c", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("//aa/bb/cc$c/dd", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("//aa/bb", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("//aa/bb/cc//dd", preserveUnc, false, func);
		CreateTestDataWithParentDirs("//aa/bb/cc\\/dd", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("//aa/bb/cc//dd", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("//aa/bb/cc/dd", preserveUnc, false, func);
		CreateTestDataWithParentDirs("//aa/bb/cc/\\dd", preserveUnc, false, func);
		CreateTestDataWithParentDirs("//aa/../", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("//aa//", preserveUnc, false, func, 0);
		CreateTestDataWithParentDirs("//aa/bb..", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("//aa/bb../", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("/\\\\aa/bb/cc/..", preserveUnc, true, func);

		CreateTestDataWithParentDirs("c:aa\\bb/cc", preserveUnc, false, func);
		CreateTestDataWithParentDirs("c:\\//\\aa\\bb", preserveUnc, false, func, 1);

		CreateTestDataWithParentDirs("mount://////a/bb/c", preserveUnc, true, func, 2);

		CreateTestDataWithParentDirs("//", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("//a", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("//a", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("//a/", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("//a/b", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("//a/b/", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("//a/b/c", preserveUnc, false, func, 2);
		CreateTestDataWithParentDirs("//a/b/c/", preserveUnc, false, func, 2);

		CreateTestDataWithParentDirs("\\\\", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("\\\\a", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("\\\\a/", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("\\\\a/b", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("\\\\a/b/", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("\\\\a/b/c", preserveUnc, false, func, 2);
		CreateTestDataWithParentDirs("\\\\a/b/c/", preserveUnc, false, func, 2);

		CreateTestDataWithParentDirs("\\\\", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("\\\\a", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("\\\\a\\", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("\\\\a\\b", preserveUnc, false, func, 1);
		CreateTestDataWithParentDirs("\\\\a\\b\\", preserveUnc, false, func, 1); // "\\a\b\/../.." crashes nnsdk
		CreateTestDataWithParentDirs("\\\\a\\b\\c", preserveUnc, false, func, 2);
		CreateTestDataWithParentDirs("\\\\a\\b\\c\\", preserveUnc, false, func, 2);
	}

	svcOutputDebugString(Buf, BufPos);
}

extern "C" void nnMain(void) {
	//setAllocators();
	nn::fs::detail::IsEnabledAccessLog(); // Adds the sdk version to the output when not calling setAllocators
	CreateTestData(CreateNormalizeTestData);
	CreateTestData(CreateIsNormalizedTestData);
}