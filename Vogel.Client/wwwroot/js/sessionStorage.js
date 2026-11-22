window.vogelSessionStorage = {
    get: function (key) {
        try {
            return window.sessionStorage.getItem(key);
        } catch (e) {
            return null;
        }
    },
    set: function (key, value) {
        try {
            window.sessionStorage.setItem(key, value);
            return true;
        } catch (e) {
            return false;
        }
    },
    remove: function (key) {
        try {
            window.sessionStorage.removeItem(key);
        } catch (e) {
            // Ignorar si sessionStorage no está disponible
        }
    },
    clear: function () {
        try {
            window.sessionStorage.clear();
        } catch (e) {
            // Ignorar si sessionStorage no está disponible
        }
    }
};
