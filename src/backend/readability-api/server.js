const express = require('express');
const { Readability } = require('@mozilla/readability');
const { JSDOM } = require('jsdom');

const app = express();
// 取消 JSON body 大小限制
app.use(express.json({ limit: 'infinity' }));

const DEBUG_MODE = process.env.DEBUG === 'true' || false


app.post('/extract', async (req, res) => {
  console.log("req.body", req.body);
  const { content, url , isNewsletter } = req.body;
  if (!content) return res.status(400).json({ error: 'Missing URL' });
  isNewsletter  ??= false
  try {
    const doc = new JSDOM(content, {});
    const reader = new Readability(doc.window.document, {
        debug: DEBUG_MODE,
        // createImageProxyUrl: null,
        keepTables: isNewsletter,
        ignoreLinkDensity: isNewsletter,
        url,
      });
    const article = reader.parse();
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
