/*
 * The slate board on the home page: one made-up beaver's working shift, drawn from the mod's rules. Its Hunger bar is
 * 13.5 hours from empty at the start of a 16-hour shift and falls an hour an hour. With the mod it keeps a 3-hour buffer:
 * if food is within half an hour's walk it tops off before work, otherwise it leaves early enough to arrive with the
 * buffer in hand. The game's own timing works until the bar is empty, then walks in the penalty (25% slower). An arrow
 * marks each meal; how far the bar refills depends on the food, so the board does not draw it.
 * Moving the slider changes the walk and redraws the board. The page is written with the one-hour walk already drawn,
 * so nothing is lost without this file. It uses only getElementById, setAttribute, textContent and one listener on
 * document, which is all the site test's stub page offers.
 */
(function () {
  'use strict';
  var START = 13.5, BUFFER = 3, SHIFT = 16, TOP_OFF_WALK = 0.5;

  function X(h) { return (70 + Math.min(h, SHIFT) * 33.125).toFixed(1); }
  function Y(v) { return (290 - Math.max(0, Math.min(v, SHIFT)) * 15).toFixed(1); }
  function pt(h, v) { return X(h) + ' ' + Y(v); }
  function clock(h) { var m = Math.round(h * 60); return Math.floor(m / 60) + ':' + ('0' + (m % 60)).slice(-2); }

  function plan(walk) {
    var p = {}, eatAt, leave;
    if (walk <= TOP_OFF_WALK) {
      leave = 0; eatAt = walk;
      p.mod = 'M' + pt(0, START) + ' L' + pt(0, START);
      p.leaveText = '';
      p.read = 'Food is ' + Math.round(walk * 60) + ' minutes away and the bar would not last the shift plus the buffer, so it tops off before work, at the start of the shift.';
    } else {
      leave = START - BUFFER - walk; eatAt = START - BUFFER;
      p.mod = 'M' + pt(0, START) + ' L' + pt(leave, START - leave);
      p.leaveText = 'leaves ' + clock(leave);
      p.read = 'It leaves ' + clock(leave) + ' into the shift, walks ' + clock(walk) + ', and eats with ' + clock(BUFFER) + ' still in hand.';
    }
    var atEat = START - eatAt;
    p.walk = 'M' + pt(leave, START - leave) + ' L' + pt(eatAt, atEat);
    // the moment it eats is marked with an arrow; how far the bar refills depends on the food, so it is not drawn
    p.eat = 'M' + pt(eatAt, atEat) + ' L' + pt(eatAt, atEat + 3.4);
    // the moment it leaves goes on the event row under the axis, with a chalk line down to it
    p.leaveX = X(leave); p.leaveY = '338';
    p.drop = leave === 0 ? 'M0 0' : 'M' + pt(leave, START - leave) + ' L' + X(leave) + ' 296';
    p.eatX = (+X(eatAt) + 8).toFixed(1); p.eatY = (+Y(atEat + 3.4) + 4).toFixed(1);
    p.eatText = (leave === 0 ? 'tops off before work, ' : 'eats, ') + clock(atEat) + ' left';

    var penaltyWalk = walk / 0.75, gameEat = START + penaltyWalk, penEnd = Math.min(gameEat, SHIFT);
    p.game = 'M' + pt(0, START) + ' L' + pt(START, 0);
    p.penalty = 'M' + pt(START, 0) + ' L' + pt(penEnd, 0);
    p.gameEat = gameEat <= SHIFT ? 'M' + pt(gameEat, 0) + ' L' + pt(gameEat, 3.4) : 'M0 0';
    p.penX = X((START + penEnd) / 2); p.penY = '338';
    p.gameRead = 'The game’s own timing: it works until the bar is empty ' + clock(START) + ' into the shift, then walks ' +
      clock(penaltyWalk) + ' in the penalty, at three-quarter speed' + (gameEat > SHIFT ? ', and is still walking when the whistle goes.' : '.');
    p.out = clock(walk);
    return p;
  }

  function set(id, attr, value) { var el = document.getElementById(id); if (el) el.setAttribute(attr, value); }
  function say(id, text) { var el = document.getElementById(id); if (el) el.textContent = text; }

  function draw(minutes) {
    var p = plan(minutes / 60);
    set('sh-mod', 'd', p.mod); set('sh-walk', 'd', p.walk); set('sh-eat', 'd', p.eat); set('sh-drop', 'd', p.drop);
    set('sh-game', 'd', p.game); set('sh-penalty', 'd', p.penalty); set('sh-game-eat', 'd', p.gameEat);
    set('sh-leave', 'x', p.leaveX); set('sh-leave', 'y', p.leaveY); say('sh-leave', p.leaveText);
    set('sh-eat-label', 'x', p.eatX); set('sh-eat-label', 'y', p.eatY); say('sh-eat-label', p.eatText);
    set('sh-pen-label', 'x', p.penX); set('sh-pen-label', 'y', p.penY);
    say('sh-out', p.out); say('sh-read-mod', p.read); say('sh-read-game', p.gameRead);
  }

  document.addEventListener('input', function (e) {
    var t = e && e.target;
    if (t && t.getAttribute && t.getAttribute('id') === 'sh-walk-range') draw(+t.value || +t.getAttribute('value'));
  });
  var range = document.getElementById('sh-walk-range'), controls = document.getElementById('sh-controls');
  if (range && controls) { controls.removeAttribute('hidden'); draw(+(range.value || range.getAttribute('value') || 60)); }
})();
