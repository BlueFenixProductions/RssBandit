using System.Text.Json;
using NUnit.Framework;
using RssBandit.ViewModels;

namespace RssBandit.ViewModels.Tests
{
    /// <summary>
    /// Pins the pure Flyweight reader core: <see cref="ReaderHtml.BuildShell"/> assembles the
    /// load-once shell (inlined assets, base64 @font-face, a #content host and a renderArticle()
    /// function); <see cref="ReaderHtml.RenderScript"/> produces the per-article body-swap call.
    /// These are platform-free so they live with the portable view-models.
    /// </summary>
    [TestFixture]
    public sealed class ReaderHtmlTests
    {
        private const string ThemeCss = ".sentinel-hljs-theme{color:#abcdef}";
        private const string HljsJs = "/*SENTINEL_HLJS*/var __hljs_sentinel=1;";
        private const string RegB64 = "REGULARFONTBASE64";
        private const string BoldB64 = "BOLDFONTBASE64";

        private static string Shell(int fontSize = 16) =>
            ReaderHtml.BuildShell(ThemeCss, HljsJs, RegB64, BoldB64, fontSize);

        [Test]
        public void BuildShell_InlinesThemeAndScriptVerbatim()
        {
            var shell = Shell();
            Assert.That(shell, Does.Contain(ThemeCss), "hljs theme CSS must be inlined, not linked.");
            Assert.That(shell, Does.Contain(HljsJs), "highlight.js must be inlined, not <script src>.");
        }

        [Test]
        public void BuildShell_InlinesHackFontsAsBase64DataUris()
        {
            var shell = Shell();
            Assert.That(shell, Does.Contain("data:font/ttf;base64," + RegB64), "regular Hack font must be a data URI.");
            Assert.That(shell, Does.Contain("data:font/ttf;base64," + BoldB64), "bold Hack font must be a data URI.");
            Assert.That(shell, Does.Contain("font-weight:bold"), "bold @font-face must declare font-weight:bold.");
        }

        [Test]
        public void BuildShell_HasNoFileOrAndroidAssetReferences()
        {
            var shell = Shell();
            // The whole point of inlining: nothing the WebView must fetch from a blocked origin.
            Assert.That(shell, Does.Not.Contain("file://"));
            Assert.That(shell, Does.Not.Contain("android_asset"));
        }

        [Test]
        public void BuildShell_HostsContentAndDefinesRenderArticleViaInnerText()
        {
            var shell = Shell();
            Assert.That(shell, Does.Contain("id=\"content\""), "shell must host an #content article element.");
            Assert.That(shell, Does.Contain("renderArticle"), "shell must define the body-swap entry point.");
            Assert.That(shell, Does.Contain("innerText"), "recovery must use innerText (honors <br>), not textContent.");
            Assert.That(shell, Does.Contain("hljs"), "shell must invoke highlight.js on the recovered source.");
        }

        [Test]
        public void BuildShell_AppliesFontSizeAndHackFamily()
        {
            Assert.That(Shell(22), Does.Contain("font-size:22px"));
            Assert.That(Shell(), Does.Contain("font-family:'Hack'"));
        }

        [Test]
        public void RenderScript_EmitsRenderArticleWithBodyThenBase()
        {
            var body = "<p>hi</p>";
            var url = "https://chris.pelatari.com/posts/x";
            // The exact call the WebView evaluates: body then base, both JSON-encoded so neither can break out.
            Assert.That(ReaderHtml.RenderScript(body, url),
                Is.EqualTo($"renderArticle({JsonSerializer.Serialize(body)},{JsonSerializer.Serialize(url)})"));
        }

        [Test]
        public void RenderScript_NeutralisesScriptBreakout()
        {
            var js = ReaderHtml.RenderScript("</script><img src=x onerror=alert(1)>", "https://x/p");
            // Must not carry a raw </script> that could close a hosting script element.
            Assert.That(js, Does.Not.Contain("</script>"));
        }

        [Test]
        public void RenderScript_NullArgs_EmitEmptyStrings()
        {
            Assert.That(ReaderHtml.RenderScript(null, null), Is.EqualTo("renderArticle(\"\",\"\")"));
        }

        [Test]
        public void BuildShell_AbsolutizesImagesAndHidesBrokenOnes()
        {
            var shell = Shell();
            Assert.That(shell, Does.Contain("new URL("), "img src must be absolutized against the article base.");
            Assert.That(shell, Does.Contain("addEventListener('error'"), "broken images must be caught (capturing).");
            Assert.That(shell, Does.Contain("display='none'"), "broken images must be hidden.");
        }
    }
}
