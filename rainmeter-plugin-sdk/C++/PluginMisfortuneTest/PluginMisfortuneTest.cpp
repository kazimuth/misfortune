#include "stdafx.h"
#include "CppUnitTest.h"
#include "../PluginMisfortune/FortuneDB.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace PluginMisfortuneTest
{		
	TEST_CLASS(TokenizerTest)
	{
	public:
		
		TEST_METHOD(TestBasic)
		{
			Assert::AreEqual(0, meme());

			FortuneDB simple(L"hello\n%%\ntest\n");
			Assert::AreEqual(L"hello\n", simple[0]);

		}

	};
}