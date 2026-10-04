// Run with: node Tests/FluentLocalizer.WebApp.Tests/site.test.cjs
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const source = fs.readFileSync(path.join(__dirname, "../../Sources/FluentLocalizer.WebApp/wwwroot/js/site.js"), "utf8");

function site(saved, blocked = false, navigator = { languages: ["it-CH", "en-US"], language: "it-CH" }) {
    const values = new Map([["fluentlocalizer.language", saved]]);
    const context = {
        navigator,
        window: {
            matchMedia: () => ({ matches: false, addEventListener() {} }),
            localStorage: {
                getItem(key) { if (blocked) throw new Error("Storage denied"); return values.get(key); },
                setItem(key, value) { if (blocked) throw new Error("Storage denied"); values.set(key, value); }
            }
        },
        document: { documentElement: { dataset: {} }, addEventListener() {} }
    };
    vm.runInNewContext(source, context);
    return context.window.fluentSite;
}

assert.deepEqual(Array.from(site().getLanguages()), ["it-CH", "en-US"]);
for (const language of ["en", "it", "fr", "de", "es"]) {
    const app = site();
    app.saveLanguage(language);
    assert.deepEqual(Array.from(app.getLanguages()), [language, "it-CH", "en-US"]);
    assert.deepEqual(Array.from(site(language).getLanguages()), [language, "it-CH", "en-US"]);
    app.saveLanguage("ja");
    assert.equal(app.getLanguages()[0], language);
}
assert.deepEqual(Array.from(site("invalid").getLanguages()), ["it-CH", "en-US"]);
const blocked = site("fr", true);
assert.doesNotThrow(() => blocked.saveLanguage("de"));
assert.deepEqual(Array.from(blocked.getLanguages()), ["it-CH", "en-US"]);
assert.deepEqual(Array.from(site(null, false, { language: "es-MX" }).getLanguages()), ["es-MX"]);
console.log("Site language preferences passed.");
