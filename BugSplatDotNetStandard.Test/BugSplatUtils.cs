﻿using BugSplatDotNetStandard.Utils;
using NUnit.Framework;

namespace Tests
{
    [TestFixture]
    public class BugSplatUtilsTest
    {

        [Test]
        public void BugSplatUtils_GetStringValueOrDefault_ShouldReturnValueIfNotNullOrEmpty()
        {
            var value = "BugSplat";
            var defaultValue = "SplatBugs";

            var result = StringUtils.GetStringValueOrDefault(value, defaultValue);

            Assert.AreEqual(value, result);
        }

        [Test]
        public void BugSplatUtils_GetStringValueOrDefault_ShouldReturnDefaultValueIfValuesIsNullOrEmpty()
        {
            string value = null;
            var defaultValue = "SplatBugs";

            var result = StringUtils.GetStringValueOrDefault(value, defaultValue);

            Assert.AreEqual(defaultValue, result);
        }
    }
}
