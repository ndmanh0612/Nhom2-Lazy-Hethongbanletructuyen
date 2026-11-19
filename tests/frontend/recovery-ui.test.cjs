const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../../src/Host/PhoneStore.Api/wwwroot/assets/js/account-management.js'), 'utf8');

function page(request) {
    const nodes = new Map();
    const calls = [], toasts = [], sessions = [];
    for (const id of ['recovery-request', 'recovery-verify', 'recovery-complete', 'recovery-status']) {
        const node = { hidden: id === 'recovery-verify' || id === 'recovery-complete', textContent: '', button: { disabled: false },
            addEventListener(type, callback) { this[type] = callback; }, querySelector() { return this.button; },
            reset() { for (const name of ['code', 'newPassword', 'confirmation']) if (this[name]) this[name].value = ''; }
        };
        for (const name of ['email', 'channel', 'code', 'newPassword', 'confirmation']) node[name] = { value: '' };
        nodes.set(id, node);
    }
    const context = {
        URLSearchParams,
        document: { getElementById: id => nodes.get(id) },
        window: { location: { hash: '#/quen-mat-khau' } },
        UI: { escapeHtml: value => value, render() {}, toast: (message, status) => toasts.push({ message, status }) },
        Api: { request: async (url, options) => { calls.push({ url, ...options }); return request(url, options); },
            setSession: (...args) => sessions.push(args), getUser: () => ({ role: 'CUSTOMER' }) }
    };
    vm.runInNewContext(source + ';globalThis.management = AccountManagement;', context);
    context.management.recovery();
    const form = id => nodes.get(id);
    form('recovery-request').email.value = 'owner@example.com';
    form('recovery-request').channel.value = 'email';
    return { ...context, calls, toasts, sessions, form, submit: id => form(id).submit({ preventDefault() {} }) };
}
function responder(url) {
    if (url.endsWith('/forgot-password')) return { challengeId: 'test-challenge', message: 'Generic status' };
    if (url.endsWith('/verify-reset-otp')) return { resetToken: 'test-reset-secret' };
    return { success: true };
}

test('recovery submits new password with verified token and clears session after success', async () => {
    const ui = page(responder);
    await ui.submit('recovery-request');
    assert.equal(ui.form('recovery-verify').hidden, false);
    ui.form('recovery-verify').code.value = '123456';
    await ui.submit('recovery-verify');
    assert.equal(ui.form('recovery-complete').hidden, false);
    assert.equal(ui.form('recovery-verify').code.value, '');
    ui.form('recovery-complete').newPassword.value = 'NewPassword123';
    ui.form('recovery-complete').confirmation.value = 'NewPassword123';
    await ui.submit('recovery-complete');
    assert.equal(ui.calls.length, 3);
    assert.equal(ui.calls[2].body.newPassword, 'NewPassword123');
    assert.equal(ui.calls[2].body.resetToken, 'test-reset-secret');
    assert.equal(ui.calls[2].body.challengeId, 'test-challenge');
    assert.equal(ui.window.location.hash, '#/dang-nhap');
    assert.deepEqual(ui.sessions[0], [null, null]);
    assert.equal(ui.form('recovery-complete').newPassword.value, '');
});

test('wrong OTP cannot advance to password entry', async () => {
    const ui = page(url => { if (url.endsWith('/verify-reset-otp')) throw new Error('Invalid OTP'); return responder(url); });
    await ui.submit('recovery-request');
    await ui.submit('recovery-verify');
    assert.equal(ui.form('recovery-complete').hidden, true);
    assert.equal(ui.toasts.at(-1).message, 'Invalid OTP');
    assert.equal(ui.sessions.length, 0);
});

test('mismatched passwords cannot reach the reset API', async () => {
    const ui = page(responder);
    await ui.submit('recovery-request'); await ui.submit('recovery-verify');
    ui.form('recovery-complete').newPassword.value = 'NewPassword123';
    ui.form('recovery-complete').confirmation.value = 'Different123';
    await ui.submit('recovery-complete');
    assert.equal(ui.calls.length, 2);
    assert.equal(ui.sessions.length, 0);
});

test('resend cooldown preserves the current challenge without another delivery request', async () => {
    const ui = page(responder);
    await ui.submit('recovery-request'); await ui.submit('recovery-request');
    assert.equal(ui.calls.length, 1);
    assert.match(ui.toasts.at(-1).message, /chờ/);
    await ui.submit('recovery-verify');
    assert.equal(ui.calls[1].body.challengeId, 'test-challenge');
});

test('unconfigured delivery shows the server failure without claiming OTP delivery', async () => {
    const ui = page(() => { throw new Error('Recovery is not configured'); });
    await ui.submit('recovery-request');
    assert.equal(ui.form('recovery-verify').hidden, true);
    assert.equal(ui.form('recovery-status').textContent, '');
    assert.equal(ui.toasts.at(-1).status, 'error');
});

test('customer cannot trigger the admin screens from client navigation', async () => {
    const ui = page(responder);
    await ui.management.customers(); await ui.management.audit();
    assert.equal(ui.calls.length, 0);
});
