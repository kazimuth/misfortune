#include "FortuneDB.h"

FortuneDB::FortuneDB() : fortunes()
{

}

FortuneDB::~FortuneDB()
{

}

wchar_t* FortuneDB::operator[](const int index)
{
	return NULL;
}

wchar_t* FortuneDB::random()
{
	return NULL;
}

int meme()
{
	return 0;
}

Fortune::Fortune(LPCWSTR contents)
{
	_contents = contents;
	_length = _width = _height = 0;
}
