const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../../src/Host/PhoneStore.Api/wwwroot/assets/js/api.js'), 'utf8');
function response(status, payload) {
    return new Response(JSON.stringify(payload), { status, headers: { 'Content-Type': 'application/json' } });
}
function storage(values = {}) {
    const data = new Map(Object.entries(values));
    return { getItem: key => data.get(key) ?? null, setItem: (key, value) => data.set(key, value), removeItem: key => data.delete(key) };
}
function client(fetch, token = 'old') {
    const context = {
        fetch, FormData, URLSearchParams,
        sessionStorage: storage(token ? { 'phonestore.accessToken': token } : {}),
        localStorage: storage({ 'phonestore.user': JSON.stringify({ id: 1, role: 'CUSTOMER', fullName: 'Old' }) }),
        document: { cookie: 'phonestore_csrf=csrf', dispatchEvent() {} },
        CustomEvent: class { constructor(type) { this.type = type; } }
    };
    vm.runInNewContext(source + ';globalThis.api = Api;', context);
    return context.api;
}
const refreshed = () => response(200, { data: { accessToken: 'new', userId: 1, email: 'a@example.com', fullName: 'Name', role: 'CUSTOMER' } });

test('invalid credentials retain the server error without attempting refresh', async () => {
    const calls = [];
    const api = client(async url => { calls.push(url); return response(401, { code: 'INVALID_CREDENTIALS', message: 'Wrong credentials' }); }, null);
    await assert.rejects(api.auth.login('a@example.com', 'wrong'), error => error.code === 'INVALID_CREDENTIALS' && error.message === 'Wrong credentials');
    assert.equal(calls.length, 1);
    assert.equal(api.getAccessToken(), null);
});

test('protected requests retry once, then invalidate a rejected renewed session', async () => {
    let calls = 0;
    let refreshes = 0;
    const api = client(async url => {
        calls++;
        if (url.endsWith('/auth/refresh')) { refreshes++; return refreshed(); }
        return response(401, { code: 'UNAUTHORIZED' });
    });
    await assert.rejects(api.auth.profile(), error => error.status === 401);
    assert.equal(refreshes, 1);
    assert.equal(calls, 3);
    assert.equal(api.getAccessToken(), null);
});

test('concurrent expired requests share one refresh operation', async () => {
    let refreshes = 0;
    const api = client(async (url, options) => {
        if (url.endsWith('/auth/refresh')) {
            refreshes++;
            await new Promise(resolve => setTimeout(resolve, 10));
            return refreshed();
        }
        if (options.headers.Authorization === 'Bearer old') return response(401, { code: 'UNAUTHORIZED' });
        return response(200, { data: { ok: true } });
    });
    const results = await Promise.all([api.auth.profile(), api.auth.addresses()]);
    assert.equal(refreshes, 1);
    assert.ok(results.every(result => result.ok));
    assert.equal(api.getAccessToken(), 'new');
});

test('a retry preserves the original JSON body', async () => {
    const bodies = [];
    const api = client(async (url, options) => {
        if (url.endsWith('/auth/refresh')) return refreshed();
        bodies.push(options.body);
        return options.headers.Authorization === 'Bearer old'
            ? response(401, { code: 'UNAUTHORIZED' })
            : response(200, { data: { id: 1, fullName: 'Updated', role: 'CUSTOMER' } });
    });
    await api.auth.updateProfile({ fullName: 'Updated', phone: null });
    assert.deepEqual(bodies, ['{"fullName":"Updated","phone":null}', '{"fullName":"Updated","phone":null}']);
    assert.equal(api.getUser().fullName, 'Updated');
});

test('logout does not refresh a failed logout request and clears local state', async () => {
    const calls = [];
    const api = client(async url => { calls.push(url); return response(401, { code: 'UNAUTHORIZED' }); });
    await assert.rejects(api.auth.logout());
    assert.equal(calls.length, 1);
    assert.equal(api.getAccessToken(), null);
    assert.equal(api.getUser(), null);
});

test('password change clears the local session only after success', async () => {
    const good = client(async () => response(200, { success: true }));
    await good.auth.changePassword('Old@123', 'New@1234');
    assert.equal(good.getAccessToken(), null);
    const bad = client(async () => response(400, { code: 'VALIDATION_ERROR' }));
    await assert.rejects(bad.auth.changePassword('Wrong@123', 'New@1234'));
    assert.equal(bad.getAccessToken(), 'old');
});

test('a pending refresh cannot resurrect a session after logout', async () => {
    let release;
    const api = client(async url => {
        if (url.endsWith('/auth/refresh')) return new Promise(resolve => { release = resolve; });
        return response(401, { code: 'UNAUTHORIZED' });
    });
    const pending = api.auth.profile();
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(typeof release, 'function');
    api.setSession(null, null);
    release(refreshed());
    await assert.rejects(pending);
    assert.equal(api.getAccessToken(), null);
});

test('a failed refresh never retries the protected mutation', async () => {
    let mutations = 0;
    const api = client(async url => {
        if (!url.endsWith('/auth/refresh')) mutations++;
        return response(401, { code: 'UNAUTHORIZED' });
    });
    await assert.rejects(api.auth.updateProfile({ fullName: 'Name' }));
    assert.equal(mutations, 1);
});

test('frontend scripts parse after adding account and admin workflows', () => {
    const directory = path.join(__dirname, '../../src/Host/PhoneStore.Api/wwwroot/assets/js');
    for (const file of fs.readdirSync(directory).filter(name => name.endsWith('.js'))) {
        new vm.Script(fs.readFileSync(path.join(directory, file), 'utf8'), { filename: file });
    }
});
