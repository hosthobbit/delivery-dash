<?php
// Delivery Dash public scoreboard (best ever, one entry per name: only your best counts).
//   GET  ?top=20  -> {"scores":[{name, score, delivered, houses, seconds, date}, ...]}
//   POST name, events, seconds, houses, score, v
//        -> {"ok":true,"rank":N,"best":bool,"score":S} or {"ok":false,"error":"..."}
// "events" is the round as a list (D1..D4 deliveries by grade, M missed house, W window, P person,
// C crash). The score is replayed here with the game's rules (ScoreRules.cs), so a typed-in score
// or an impossible round is rejected.

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store, max-age=0');
$origin = $_SERVER['HTTP_ORIGIN'] ?? '';
if ($origin === 'https://hosthobbit.github.io'
    || preg_match('#^http://(localhost|127\.0\.0\.1|192\.168\.\d+\.\d+|10\.\d+\.\d+\.\d+)(:\d+)?$#', $origin)) {
    header('Access-Control-Allow-Origin: ' . $origin);
    header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
    header('Access-Control-Allow-Headers: Content-Type');
    header('Vary: Origin');
}
if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') { http_response_code(204); exit; }

function out($data, $code = 200) { http_response_code($code); echo json_encode($data); exit; }
function fail($error, $code = 400) { out(['ok' => false, 'error' => $error], $code); }

const HOUSES = 30;          // subscribers on the route
const PAR_SECONDS = 150;
const MIN_SECONDS = 85;     // 880 m at top speed takes ~88 s

$home = getenv('HOME') ?: dirname($_SERVER['DOCUMENT_ROOT'] ?? __DIR__);
$dataDir = getenv('DELIVERY_SCORES_DIR') ?: $home . '/game-scores';
if (!is_dir($dataDir) && !@mkdir($dataDir, 0700, true)) fail('storage unavailable', 500);
try {
    $db = new PDO('sqlite:' . $dataDir . '/delivery-scores.sqlite');
    $db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
    $db->exec('PRAGMA busy_timeout = 3000');
    $db->exec('CREATE TABLE IF NOT EXISTS scores (
        name TEXT PRIMARY KEY, score INTEGER NOT NULL, delivered INTEGER NOT NULL, houses INTEGER NOT NULL,
        seconds REAL NOT NULL, created INTEGER NOT NULL, ip TEXT NOT NULL)');
    $db->exec('CREATE INDEX IF NOT EXISTS score_rank ON scores (score DESC)');
    $db->exec('CREATE TABLE IF NOT EXISTS submits (ip TEXT NOT NULL, created INTEGER NOT NULL)');
} catch (Exception $e) {
    fail('storage unavailable', 500);
}

if ($_SERVER['REQUEST_METHOD'] === 'GET') {
    $top = max(1, min(50, (int)($_GET['top'] ?? 20)));
    $rows = $db->query("SELECT * FROM scores ORDER BY score DESC, created ASC LIMIT $top")->fetchAll(PDO::FETCH_ASSOC);
    out(['scores' => array_map(function ($r) {
        return ['name' => $r['name'], 'score' => (int)$r['score'], 'delivered' => (int)$r['delivered'],
                'houses' => (int)$r['houses'], 'seconds' => round((float)$r['seconds'], 2), 'date' => gmdate('Y-m-d', (int)$r['created'])];
    }, $rows)]);
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST') fail('method not allowed', 405);

$in = $_POST;
if (!$in) { $j = json_decode(file_get_contents('php://input', false, null, 0, 4000) ?: '', true); if (is_array($j)) $in = $j; }

if ((int)($in['v'] ?? 0) !== 1) fail('Please refresh the game to submit scores.');
$houses = filter_var($in['houses'] ?? null, FILTER_VALIDATE_INT);
if ($houses !== HOUSES) fail('bad houses');
$seconds = filter_var($in['seconds'] ?? null, FILTER_VALIDATE_FLOAT);
if ($seconds === false || $seconds < MIN_SECONDS || $seconds > 1800) fail('bad seconds');
$claimed = filter_var($in['score'] ?? null, FILTER_VALIDATE_INT);
if ($claimed === false || $claimed < 0 || $claimed > 1000000) fail('bad score');

$events = (string)($in['events'] ?? '');
if (strlen($events) > 1200 || !preg_match('/^(D[1-4]|M|W|P|C)(,(D[1-4]|M|W|P|C))*$/', $events)) fail('bad events');
$list = explode(',', $events);
$count = array_count_values($list);
$delivered = ($count['D1'] ?? 0) + ($count['D2'] ?? 0) + ($count['D3'] ?? 0) + ($count['D4'] ?? 0);
$missed = $count['M'] ?? 0;
if ($delivered + $missed !== HOUSES) fail('That round doesn\'t add up.');
if (($count['W'] ?? 0) > 60 || ($count['P'] ?? 0) > 30 || ($count['C'] ?? 0) > 60) fail('That round doesn\'t add up.');

$name = trim(preg_replace('/\s+/u', ' ', (string)($in['name'] ?? '')));
if (!preg_match("/^[\\p{L}\\p{N} '._-]{2,16}$/u", $name)) fail('Names can be 2 to 16 letters or numbers.');
$squashed = strtolower(preg_replace('/[^a-z]/i', '', strtr($name, '013457@$', 'oieastas'))) . ' '
          . strtolower(preg_replace('/[^a-z]/i', '', strtr($name, '013457@$', 'oleastas')));
foreach (['fuck', 'shit', 'cunt', 'bitch', 'bastard', 'nigg', 'fag', 'twat', 'wank', 'piss', 'cock', 'dick', 'pussy', 'slut', 'whore', 'rape', 'nazi', 'hitler', 'retard', 'arse', 'asshole', 'bollock', 'spastic', 'paki', 'kkk'] as $bad) {
    if (strpos($squashed, $bad) !== false) fail('Please pick a different name.');
}

// same rules as ScoreSheet.Replay in Assets/DeliveryDash/Scripts/Core/ScoreRules.cs
function replay($list, $seconds) {
    $base = ['D1' => 100, 'D2' => 200, 'D3' => 300, 'D4' => 400];
    $penalty = ['M' => 50, 'W' => 150, 'P' => 200, 'C' => 100];
    $score = 0; $streak = 0; $delivered = 0;
    foreach ($list as $e) {
        if (isset($base[$e])) {
            $delta = (int)round($base[$e] * min(3.0, 1.0 + 0.25 * $streak));
            $streak++; $delivered++;
        } else {
            $delta = -$penalty[$e];
            $streak = 0;
        }
        $score = max(0, $score + $delta);
    }
    $bonus = max(0, (int)floor(PAR_SECONDS - $seconds)) * 25;
    if ($delivered === HOUSES) $bonus += 1000;
    return $score + $bonus;
}
// seconds arrive rounded to 0.01, which can tip the time bonus over a whole second either way
$ok = false;
foreach ([0, -0.006, 0.006] as $nudge) if (replay($list, $seconds + $nudge) === $claimed) { $ok = true; break; }
if (!$ok) fail('score mismatch');
$score = $claimed;

$ip = hash('sha256', ($_SERVER['REMOTE_ADDR'] ?? '') . '|delivery-scores');
$db->prepare('DELETE FROM submits WHERE created < ?')->execute([time() - 3600]);
$recent = $db->prepare('SELECT COUNT(*) FROM submits WHERE ip = ?');
$recent->execute([$ip]);
if ((int)$recent->fetchColumn() >= 30) fail('Too many scores this hour, try again later.', 429);
$db->prepare('INSERT INTO submits (ip, created) VALUES (?, ?)')->execute([$ip, time()]);

$prev = $db->prepare('SELECT score FROM scores WHERE name = ?');
$prev->execute([$name]);
$old = $prev->fetchColumn();
$best = $old === false || $score > (int)$old;
if ($best) {
    $db->prepare('INSERT OR REPLACE INTO scores (name, score, delivered, houses, seconds, created, ip) VALUES (?, ?, ?, ?, ?, ?, ?)')
       ->execute([$name, $score, $delivered, HOUSES, round($seconds, 2), time(), $ip]);
}
$mine = $best ? $score : (int)$old;
$rank = $db->prepare('SELECT COUNT(*) FROM scores WHERE score > ?');
$rank->execute([$mine]);
out(['ok' => true, 'rank' => (int)$rank->fetchColumn() + 1, 'best' => $best, 'score' => $mine]);
