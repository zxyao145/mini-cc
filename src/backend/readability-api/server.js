const express = require('express');
// const { Readability } = require('@mozilla/readability');
var Readability = require("./Readability");
const { JSDOM } = require('jsdom');
const crypto = require('crypto')
const { encode } = require('urlsafe-base64')


const app = express();
// 取消 JSON body 大小限制
app.use(express.json({ limit: 'infinity' }));

const DEBUG_MODE = process.env.DEBUG === 'true' || false

// const proxyUrl = "http://localhost:5001/imageProxy";

const signImageProxyUrl = (url) => {
  const secretKey = process.env.IMAGE_PROXY_SECRET || "my-secret-key-123!@#";
  console.log("secretKey", secretKey, process.env.IMAGE_PROXY_SECRET);
  return encode(
    crypto.createHmac('sha256', secretKey).update(url).digest()
  )
}

const createImageProxyUrl = (url, proxyUrl, secretKey,  width = 0, height = 0) => {
  if (url.startsWith(proxyUrl)) {
    return url
  }
  
  const urlWithOptions = `${url}#${width}x${height}`
  const signature = signImageProxyUrl(urlWithOptions)

  return `${proxyUrl}/${width}x${height},s${signature}/${url}`
}

app.post('/extract', async (req, res) => {
  console.log("req.body", req.body);
  const { content, url, proxyUrl, secretKey, isNewsletter } = req.body;
  if (!content) return res.status(400).json({ error: 'Missing URL' });
  isNewsletter  ??= false
  try {
    const doc = new JSDOM(content, {});
    const reader = new Readability(doc.window.document, {
      debug: DEBUG_MODE,
      createImageProxyUrl: (url, width = 0, height = 0) => {
        return createImageProxyUrl(url, proxyUrl, secretKey, width, height);
      },
      keepTables: isNewsletter,
      ignoreLinkDensity: isNewsletter,
      minContentLength: 0,
      url,
    });
    const article = await reader.parse();
    res.json(article);
  } catch (err) {
    console.error('Extraction error:', err.message);
    res.status(500).json({ error: 'Extraction failed' });
  }
});

const PORT = process.env.PORT || 5002;
app.listen(PORT, () => {
  console.log(`✅ Readability API running on http://localhost:${PORT}`);
});
