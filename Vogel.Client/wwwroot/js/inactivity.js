window.vogelInactivity = (function () {
    let _dotNetRef = null;
    let _handler = null;
    const _events = ['mousemove', 'mousedown', 'keydown', 'touchstart', 'scroll'];

    return {
        init: function (dotNetRef) {
            this.dispose();
            _dotNetRef = dotNetRef;
            let lastSent = 0;

            _handler = function () {
                const now = Date.now();
                if (now - lastSent > 1000 && _dotNetRef) {
                    lastSent = now;
                    _dotNetRef.invokeMethodAsync('OnUserActivity').catch(function () { });
                }
            };

            _events.forEach(function (ev) {
                window.addEventListener(ev, _handler, { passive: true });
            });
        },
        dispose: function () {
            if (_handler) {
                _events.forEach(function (ev) {
                    window.removeEventListener(ev, _handler);
                });
                _handler = null;
            }
            _dotNetRef = null;
        }
    };
})();
