#pragma once
#include <string>
#include <vector>
#include <memory>


#include <boost/multi_index_container.hpp>
#include <boost/pool/pool_alloc.hpp>
#include <Windows.h>

#define TEST_EXPORT __declspec(dllexport)

namespace tags {
	struct main {};
	struct length {};
	struct width {};
	struct height {};
};

class Fortune {
	LPCWSTR _contents;
	std::shared_ptr < std::wstring > _filename;
	int _length;
	int _width;
	int _height;

public:
	/// Takes ownership of contents. Does not take ownership of filename.
	Fortune(LPCWSTR contents);
	int contents();
	int length();
	int width();
	int height();
};

class FortuneDB {
	std::vector<std::wstring> fortunes;

public:
	TEST_EXPORT FortuneDB();
	TEST_EXPORT ~FortuneDB();

	TEST_EXPORT wchar_t* operator[] (const int index);
	TEST_EXPORT wchar_t* random();
};