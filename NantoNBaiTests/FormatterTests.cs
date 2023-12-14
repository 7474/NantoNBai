using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NantoNBai.Tests
{
    [TestClass()]
    public class FormatterTests
    {
        [TestMethod()]
        public void FormatTest()
        {
            var formatter = new Formatter();

            Assert.AreEqual("1倍", formatter.Format(1, 1, Nan.Bai));
            Assert.AreEqual("2倍", formatter.Format(1, 2, Nan.Bai));
            Assert.AreEqual("0倍", formatter.Format(2, 1, Nan.Bai));
            Assert.AreEqual("∞倍", formatter.Format(0, 1, Nan.Bai));

            Assert.AreEqual("100%", formatter.Format(1, 1, Nan.Pasento));
            Assert.AreEqual("200%", formatter.Format(1, 2, Nan.Pasento));
            Assert.AreEqual("50%", formatter.Format(2, 1, Nan.Pasento));
            Assert.AreEqual("0.9%", formatter.Format(101, 1, Nan.Pasento));
            Assert.AreEqual("0.1%", formatter.Format(1000, 1, Nan.Pasento));
            Assert.AreEqual("0.09%", formatter.Format(1001, 1, Nan.Pasento));
            //Assert.AreEqual("0%", formatter.Format(double.MaxValue, 1 / double.MaxValue, Nan.Pasento));
            Assert.AreEqual("∞%", formatter.Format(0, 1, Nan.Pasento));

            Assert.AreEqual("1/1", formatter.Format(1, 1, Nan.Bunno));
            Assert.AreEqual("2/1", formatter.Format(1, 2, Nan.Bunno));
            Assert.AreEqual("1/2", formatter.Format(2, 1, Nan.Bunno));
            Assert.AreEqual("1/101", formatter.Format(101, 1, Nan.Bunno));
            Assert.AreEqual("1/4", formatter.Format(100, 25, Nan.Bunno));
            Assert.AreEqual("4/1", formatter.Format(25, 100, Nan.Bunno));
            Assert.AreEqual("∞", formatter.Format(0, 1, Nan.Bunno));
        }
    }
}