using Utilities;
using Xunit;
using Microsoft.AspNetCore.Mvc;


namespace UrlShortener.Tests
{
    public class Url64HelperTests
    {
        [Fact]
        public void Encode_SameInput_ShouldReturnSameOutput()
        {
            //Arrange
            var url = "http://www.testurl.com";
            
            //Act
            var code1 = Url64Helper.Encode(url);
            var code2 = Url64Helper.Encode(url);

            //Assert
            Assert.Equal(code1, code2);
        }

        [Fact]
        public void Encode_DifferentInput_ShouldReturnDifferentOutput()
        {
            //Arrange
            var url1 = "http://www.testurl.com";
            var url2 = "http://www.sampleurl.com";

            //Act
            var code1 = Url64Helper.Encode(url1);
            var code2 = Url64Helper.Encode(url2);

            //Assert
            Assert.NotEqual(code1, code2);
        }

        [Fact]
        public void Encode_ShouldReturnNonEmptyString()
        {
            //Arrange
            var url = "http://www.sampleurl.com";

            //Act
            var code = Url64Helper.Encode(url);

            //Assert
            Assert.NotEmpty(code);
        }

        [Fact]
        public void Encode_EmptyInput_ShouldReturnNullArgumentException()
        {
            //Arrange
            var emptyUrl = "";

            //Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => Url64Helper.Encode(emptyUrl));
            Assert.Contains("Input cannot be empty", ex.Message);
        }

    }
}