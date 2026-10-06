'use strict';

const fs = require('node:fs');
const path = require('node:path');

function readText(file) {
    const bytes = fs.readFileSync(file);
    // Windows PowerShell 5.1 Tee-Object writes UTF-16LE logs; PS7 writes UTF-8.
    if (bytes[0] === 0xff && bytes[1] === 0xfe) return bytes.subarray(2).toString('utf16le');
    return bytes.toString('utf8').replace(/^\uFEFF/, '');
}

function requireContract(condition, message) {
    if (!condition) throw new Error(message);
}

// Keep offsets intact so braces inside comments and literals cannot alter body extraction.
function sanitize(code, keepStrings = false) {
    return code.replace(/\/\*[\s\S]*?\*\/|\/\/[^\r\n]*|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'/g,
        token => keepStrings && (token[0] === '"' || token.startsWith('@"'))
            ? token : token.replace(/[^\r\n]/g, ' '));
}

function readBody(code, signature) {
    const masked = sanitize(code);
    const match = signature.exec(masked);
    requireContract(match, 'Missing declaration: ' + signature);
    const start = masked.indexOf('{', match.index + match[0].length);
    requireContract(start >= 0, 'Missing body: ' + signature);
    let depth = 0;
    for (let i = start; i < masked.length; i++) {
        if (masked[i] === '{') depth++;
        if (masked[i] === '}' && --depth === 0) return code.slice(start, i + 1);
    }
    throw new Error('Unclosed body: ' + signature);
}

function statements(body) {
    const code = sanitize(body);
    let depth = 0, parens = 0, start = 1;
    const result = [];
    for (let i = 0; i < code.length; i++) {
        const c = code[i];
        if (c === '(') parens++;
        if (c === ')') parens--;
        if (c === '{') depth++;
        if (c === '}') {
            depth--;
            if (depth === 1 && parens === 0) {
                result.push(code.slice(start, i + 1).replace(/\s+/g, ''));
                start = i + 1;
            }
        }
        if (c === ';' && depth === 1 && parens === 0) {
            result.push(code.slice(start, i + 1).replace(/\s+/g, ''));
            start = i + 1;
        }
    }
    return result.filter(Boolean).map(s => s.replace(/\b(?:this|base)\./g, ''));
}

function checkMissionTick(code) {
    const body = readBody(code, /\bOnMissionTick\s*\([^)]*\)/);
    const clean = sanitize(body);
    const items = statements(body);
    const route = items.findIndex(s => /^ApplyRouteASingle\(\w+,CbRouteAPoC\.Enabled\);$/.test(s));
    const executor = items.findIndex(s => /^_commandExecutor\.Tick\([^;{}]+\);$/.test(s));
    requireContract((clean.match(/\bApplyRouteASingle\s*\(/g) || []).length === 1
        && (clean.match(/_commandExecutor\.Tick\s*\(/g) || []).length === 1
        && route >= 0 && executor > route,
    'Mission tick requires one direct Route A call followed by one direct executor call');
    const bridge = /^(?:_bench\.(?:BeginSection|EndSection)\(\);|if\(CbRouteAPoC\.Control\.Target!=null\)(?:\{)?_commandExecutor\.ForgetMovement\([^;{}]+\);(?:\})?)$/;
    requireContract(items.slice(route + 1, executor).every(s => bridge.test(s)),
        'Mission tick has an unapproved statement between reservation and executor');
}

const candidateOrders = {
    CbHoldBehavior: 'MovementOrder.MovementOrderStop',
    CbAdvanceBehavior: 'MovementOrder.MovementOrderAdvance',
    CbRejoinBehavior: 'MovementOrder.MovementOrderMove(Handle.Target)',
    CbRetreatBehavior: 'MovementOrder.MovementOrderRetreat'
};

function checkCandidate(code, name) {
    const body = readBody(code, /\bCalculateCurrentOrder\s*\(\s*\)/);
    const items = statements(body);
    requireContract(items.length === 2 && items[0] === 'CurrentOrder=' + candidateOrders[name] + ';'
        && items[1] === 'IsCurrentOrderChanged=false;',
    name + ' must contain only its prepared order and an unconditional final false flag');
}

function checkMcm(code) {
    const targets = {
        Cps3ValidationEnabled: 'Cps3RuntimeSettings.Enabled',
        Cps3ObserverEnabled: 'Cps3RuntimeSettings.ObserverEnabled',
        RouteAMovementEnabled: 'Cps2PoCSettings.RouteAEnabled',
        Cps3ShadowLogEnabled: 'Cps3RuntimeSettings.ShadowWouldApplyLogEnabled'
    };
    for (const [name, field] of Object.entries(targets)) {
        const body = sanitize(readBody(code, new RegExp('\\bpublic\\s+bool\\s+' + name + '\\b')))
            .replace(/\s+|CombatBrain\.Settings\./g, '');
        const target = field.replace(/\./g, '\\.');
        const expected = new RegExp('^\\{get(?:=>' + target + ';|\\{return' + target
            + ';\\})set(?:=>' + target + '=value;|\\{' + target + '=value;\\})\\}$');
        requireContract(expected.test(body), 'MCM property must read its field and write value: ' + name);
    }
    const raw = sanitize(code, true);
    const names = raw.match(/\bCps3ModeNames\s*=\s*(?:new\s+string\s*\[\s*\d*\s*\]\s*)?\{([^}]*)\}/);
    const labels = names ? [...names[1].matchAll(/"([A-E]):[^"\\]*"/g)].map(m => m[1]) : [];
    requireContract(labels.join('') === 'ABCDE'
        && names[1].replace(/"[A-E]:[^"\\]*"|[\s,]/g, '') === '',
    'MCM mode array must contain exactly five ordered A/B/C/D/E labels');
    requireContract(/new\s+Dropdown<string>\s*\(\s*Cps3ModeNames\s*,\s*0\s*\)/.test(raw),
        'MCM dropdown must use the checked mode array with index zero');
    const mode = sanitize(readBody(code, /\bpublic\s+Dropdown<string>\s+Cps3ValidationMode\b/))
        .replace(/\s+|CombatBrain\.Settings\.|\bthis\./g, '');
    const assignment = 'Cps3RuntimeSettings.Mode=Cps3RuntimeSettings.ModeFromSelectedIndex(_cps3ModeDropdown.SelectedIndex);';
    const getter = mode.match(/^\{get\{([\s\S]*?)\}set\{([\s\S]*)\}\}$/);
    const expectedGet = 'if(Cps3RuntimeSettings.NeedsModeSync(_cps3ModeDropdown.SelectedIndex))';
    requireContract(getter && (getter[1] === expectedGet + '{' + assignment + '}return_cps3ModeDropdown;'
        || getter[1] === expectedGet + assignment + 'return_cps3ModeDropdown;')
        && getter[2] === 'Cps3RuntimeSettings.Mode=Cps3RuntimeSettings.ModeFromSelectedIndex(value.SelectedIndex);'
            + '_cps3ModeDropdown.SelectedIndex=value.SelectedIndex;',
    'MCM mode property must retain instance-to-mode getter sync and setter forwarding');
}

function requiredGroups() {
    const groups = JSON.parse(readText(path.join(__dirname, 'RequiredTestGroups.json'))).RequiredGroups;
    requireContract(Array.isArray(groups) && groups.length > 0 && new Set(groups).size === groups.length,
        'Invalid required test group manifest');
    return groups;
}

function checkTestRegistration(files, required = requiredGroups()) {
    const methods = new Map();
    for (const code of files) {
        const clean = sanitize(code);
        for (const m of clean.matchAll(/\bprivate\s+static\s+void\s+(Test\w+)\s*\(\s*\)/g)) {
            requireContract(!methods.has(m[1]), 'Duplicate test method: ' + m[1]);
            methods.set(m[1], readBody(code, new RegExp('\\b' + m[1] + '\\s*\\(\\s*\\)')));
        }
    }
    requireContract([...methods.keys()].sort().join('\n') === [...required].sort().join('\n'),
        'Declared tests differ from required group manifest; update it explicitly for new tests');
    const program = files.find(s => /\bprivate\s+static\s+int\s+Main\s*\(/.test(sanitize(s)));
    requireContract(program, 'Missing regression Main');
    const pending = [readBody(program, /\bprivate\s+static\s+int\s+Main\s*\([^)]*\)/)];
    const seen = new Set();
    while (pending.length) {
        for (const m of sanitize(pending.pop()).matchAll(/\bRunTest\s*\(\s*(Test\w+)\s*\)/g)) {
            requireContract(methods.has(m[1]), 'Unknown registered test: ' + m[1]);
            if (!seen.has(m[1])) { seen.add(m[1]); pending.push(methods.get(m[1])); }
        }
    }
    requireContract(required.every(name => seen.has(name)), 'Required test is unreachable through RunTest');
}

function checkRegressionLog(log, required = requiredGroups()) {
    const completed = new Set();
    for (const line of log.split(/\r?\n/)) {
        if (!line.startsWith('[GROUP ')) continue;
        const m = line.match(/^\[GROUP PASS\] (Test\w+) checks=(\d+)$/);
        requireContract(m && Number(m[2]) > 0, 'Failed or empty test group: ' + line);
        requireContract(required.includes(m[1]) && !completed.has(m[1]), 'Unknown or duplicate group: ' + line);
        completed.add(m[1]);
    }
    requireContract(required.every(name => completed.has(name)), 'Regression log is missing a required completed test group');
}

function checkSources(repo) {
    const read = p => readText(path.join(repo, p));
    checkMissionTick(read('CombatBrain/Ai/CombatAiMissionLogic.cs'));
    const adapter = read('CombatBrain/Ai/RouteAOrderAdapter.cs');
    for (const name of Object.keys(candidateOrders)) {
        checkCandidate(readBody(adapter, new RegExp('\\bclass\\s+' + name + '\\b')), name);
    }
    checkMcm(read('CombatBrain/Mcm/CombatBrainMcmSettings.cs'));
    checkTestRegistration(fs.readdirSync(path.join(repo, 'OfflineTests')).filter(p => p.endsWith('.cs'))
        .map(p => read('OfflineTests/' + p)));
}

function checkCompiled(dir) {
    const read = type => readText(path.join(dir, type + '.cs'));
    checkMissionTick(read('CombatBrain.Ai.CombatAiMissionLogic'));
    for (const name of Object.keys(candidateOrders)) checkCandidate(read('CombatBrain.Ai.' + name), name);
    checkMcm(read('CombatBrain.Mcm.CombatBrainMcmSettings'));
}

module.exports = { sanitize, readBody, checkMissionTick, checkCandidate, checkMcm,
    checkTestRegistration, checkRegressionLog, checkSources, checkCompiled };

if (require.main === module) {
    try {
        const [mode, target] = process.argv.slice(2);
        if (mode === '--source' && target) checkSources(target);
        else if (mode === '--compiled' && target) checkCompiled(target);
        else if (mode === '--regression-log' && target) checkRegressionLog(readText(target));
        else throw new Error('Usage: node VerificationContracts.js --source REPO | --compiled DIR | --regression-log LOG');
        console.log('[PASS] Verification contracts: ' + mode);
    } catch (error) {
        console.error('[FAIL] ' + error.message);
        process.exitCode = 1;
    }
}
