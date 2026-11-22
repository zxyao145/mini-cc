using System.Text;
using MiniCc.Api.Core.ImageNs.Domain;

namespace MiniCc.Api.Test.Images;

public class ImageSnifferTests
{
    [Theory]
    [InlineData(new byte[] { 0xff, 0xd8, 0xff, 0x00 }, "jpg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }, "png")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, "gif")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, "gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, "webp")]
    [InlineData(new byte[] { 0, 0, 0, 0, 0x66, 0x74, 0x79, 0x70, 0x61, 0x76, 0x69, 0x66 }, "avif")]
    [InlineData(new byte[] { 0, 0, 0, 0, 0x66, 0x74, 0x79, 0x70, 0x61, 0x76, 0x69, 0x73 }, "avif")]
    public void Detect_RasterHeaders(byte[] bytes, string ext)
    {
        ImageSniffer.Detect(bytes).Should().Be(ext);
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>")]
    [InlineData("\uFEFF  \n<svg></svg>")]
    [InlineData("<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\"></svg>")]
    [InlineData("""<?xml version="1.0"?><!DOCTYPE svg PUBLIC "-//W3C//DTD SVG 1.1//EN" "http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd"><svg xmlns="http://www.w3.org/2000/svg"/>""")]
    public void Detect_Svg(string source)
    {
        ImageSniffer.Detect(Encoding.UTF8.GetBytes(source)).Should().Be("svg");
    }

    [Fact]
    public void Detect_HtmlNamedPng_IsRejected()
    {
        ImageSniffer.Detect(Encoding.UTF8.GetBytes("<html><body>image not found</body></html>")).Should().BeNull();
    }
}
