using System.Text.Json;

namespace RssBandit.ViewModels
{
    /// <summary>
    /// Pure, platform-free builder for the MAUI article reader's WebView content, split Flyweight-style:
    /// <see cref="BuildShell"/> assembles the heavy, immutable <em>shell</em> (Tokyo Night theme + the
    /// bundled highlight.js and Hack fonts, all inlined so nothing loads from a blocked origin) which is
    /// loaded into the WebView <em>once</em>; <see cref="RenderScript"/> produces the cheap per-article
    /// call that swaps only the body into the already-parsed shell.
    /// </summary>
    /// <remarks>
    /// The reader is a "reader mode": it imposes its own skin and re-highlights every code block from
    /// recovered source (via <c>innerText</c>, which honors legacy <c>&lt;br&gt;</c> line breaks) in its
    /// own theme, keeping only the author's <c>language-*</c> hint. Do NOT add a <c>span</c>/<c>code</c>
    /// colour <c>!important</c> rule to <see cref="ReadingThemeCss"/> — it would re-flatten highlight.js's
    /// token colours, which survive only because nothing <c>!important</c> targets them.
    /// </remarks>
    public static class ReaderHtml
    {
        // Tokyo Night reading chrome (validated across the Shiki / 2005-WLW / Prism fixtures). Forces the
        // dark surface and a fit-to-width layout over the feed's own styling; colours text elements only
        // (NOT span/pre/code) so highlight.js token colours survive.
        private const string ReadingThemeCss = @"
html,body{background:#1a1b26!important;color:#c0caf5!important;line-height:1.6;padding:4px 10px;overflow-x:hidden!important;overflow-wrap:break-word!important;word-break:break-word!important;}
body *{background-color:transparent!important;max-width:100%!important;box-sizing:border-box!important;}
body,body *{font-family:'Hack','Roboto Mono',monospace!important;}
p,div,li,td,th,blockquote,strong,em,b,i,small{color:#c0caf5!important;}
a{color:#7aa2f7!important;}
h1,h2,h3,h4{color:#bb9af7!important;}
img{width:auto!important;max-width:100%!important;height:auto!important;}
table{max-width:100%!important;table-layout:fixed!important;}
img[src*=""templates.invalid""]{display:none!important;}
pre{background-color:#16161e!important;padding:12px!important;overflow-x:auto!important;border-radius:6px!important;}
.hljs{background:#16161e!important;}
hr{border-color:#414868!important;}
blockquote{border-left:3px solid #7aa2f7!important;color:#a9b1d6!important;}";

        // Recover-and-re-highlight, operating on the #content host. renderArticle(html) is invoked once
        // per article via EvaluateJavaScriptAsync after the shell has loaded. Recovery is innerText (NOT
        // textContent) so <br>-delimited legacy blocks keep their line breaks; the author's language-*
        // hint is honoured when present, otherwise hljs auto-detects.
        private const string RenderJs = @"
function langFrom(el){if(!el||!el.classList)return null;for(var c of el.classList){if(c.indexOf('language-')===0)return c.slice(9);if(c.indexOf('lang-')===0)return c.slice(5);}return null;}
function highlightAll(){
  if(!window.hljs)return;
  document.querySelectorAll('#content pre').forEach(function(pre){
    var code=pre.querySelector('code');
    var hint=langFrom(code)||langFrom(pre)||langFrom(pre.parentElement);
    var text=(code||pre).innerText;
    if(!code){code=document.createElement('code');pre.textContent='';pre.appendChild(code);}
    code.removeAttribute('data-highlighted');
    code.className=hint?('language-'+hint):'';
    code.textContent=text;
    hljs.highlightElement(code);
  });
}
function renderArticle(html,base){var c=document.getElementById('content');if(!c)return;var d=new DOMParser().parseFromString(html||'','text/html');if(base){d.querySelectorAll('img[src]').forEach(function(im){try{im.setAttribute('src',new URL(im.getAttribute('src'),base).href);}catch(e){}});}c.innerHTML=(d&&d.body)?d.body.innerHTML:(html||'');highlightAll();}
/* Hide images that fail to load (404 feeds, dead links, broken paths) so they don't clutter the read.
   Capturing listener on document because 'error' doesn't bubble; set once, covers every rendered article. */
document.addEventListener('error',function(e){if(e.target&&e.target.tagName==='IMG')e.target.style.display='none';},true);";

        /// <summary>
        /// Builds the load-once reader shell. All assets are inlined (no <c>file://</c> / <c>android_asset</c>),
        /// so it renders identically on every head regardless of the document's origin.
        /// </summary>
        /// <param name="hljsThemeCss">Contents of the highlight.js theme (tokyo-night-dark).</param>
        /// <param name="hljsScript">Contents of highlight.min.js.</param>
        /// <param name="hackRegularBase64">Base64 of Hack-Regular.ttf.</param>
        /// <param name="hackBoldBase64">Base64 of Hack-Bold.ttf.</param>
        /// <param name="fontSizePx">Reader font size, in CSS px.</param>
        public static string BuildShell(string hljsThemeCss, string hljsScript,
            string hackRegularBase64, string hackBoldBase64, int fontSizePx)
        {
            string fontFaces =
                "@font-face{font-family:'Hack';font-style:normal;font-weight:normal;" +
                $"src:url('data:font/ttf;base64,{hackRegularBase64}') format('truetype');}}" +
                "@font-face{font-family:'Hack';font-style:normal;font-weight:bold;" +
                $"src:url('data:font/ttf;base64,{hackBoldBase64}') format('truetype');}}";

            return "<!DOCTYPE html><html><head><meta charset=\"utf-8\">" +
                   "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
                   "<style>" +
                   fontFaces +
                   hljsThemeCss +
                   ReadingThemeCss +
                   $"html,body{{font-size:{fontSizePx}px!important;}}" +
                   "</style>" +
                   "<script>" + hljsScript + "</script>" +
                   "<script>" + RenderJs + "</script>" +
                   "</head><body><article id=\"content\"></article></body></html>";
        }

        /// <summary>
        /// The per-article JS that swaps <paramref name="bodyHtml"/> into the loaded shell and re-highlights.
        /// <paramref name="baseUrl"/> (the article's link) is used to absolutize relative <c>&lt;img&gt;</c>
        /// sources — the per-article base the Flyweight shell can't carry, since one shell serves many
        /// articles. Both args are JSON-encoded (the default encoder escapes <c>&lt;</c>/<c>&gt;</c>/<c>&amp;</c>/quotes),
        /// so neither can break out of the call and the body carries no raw <c>&lt;/script&gt;</c>.
        /// </summary>
        public static string RenderScript(string? bodyHtml, string? baseUrl = null)
            => "renderArticle(" + JsonSerializer.Serialize(bodyHtml ?? string.Empty)
               + "," + JsonSerializer.Serialize(baseUrl ?? string.Empty) + ")";
    }
}
