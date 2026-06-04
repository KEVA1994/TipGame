window.supabaseRealtime = {
    _client: null,
    _channel: null,

    init: async function (url, anonKey) {
        // Wait for the ESM module to load (sets window._supabaseCreateClient)
        for (var i = 0; i < 50 && !window._supabaseCreateClient; i++) {
            await new Promise(r => setTimeout(r, 100));
        }
        if (!window._supabaseCreateClient) {
            console.error('supabase-js failed to load');
            return;
        }
        this._client = window._supabaseCreateClient(url, anonKey, {
            auth: { persistSession: false, autoRefreshToken: false, detectSessionInUrl: false }
        });
    },

    subscribeMatches: function (dotNetRef) {
        if (!this._client) return;
        this.unsubscribe();

        this._channel = this._client
            .channel('matches-changes')
            .on('postgres_changes',
                { event: '*', schema: 'public', table: 'Matches' },
                function (payload) {
                    console.log('[Realtime] Match changed:', payload.eventType, payload.new);
                    dotNetRef.invokeMethodAsync('OnMatchChanged', payload.new);
                }
            )
            .subscribe(function (status) {
                console.log('[Realtime] Subscription status:', status);
            });
    },

    unsubscribe: function () {
        if (this._channel) {
            this._client.removeChannel(this._channel);
            this._channel = null;
        }
    }
};

window.tipInputs = {
    focusById: function (id) {
        var el = document.getElementById(id);
        if (el) { el.focus(); el.select && el.select(); }
    },
    blurById: function (id) {
        var el = document.getElementById(id);
        if (el) { el.blur(); }
    },
    focusNextHome: function (currentMatchId) {
        var all = Array.prototype.slice.call(document.querySelectorAll('input[id^="tip-home-"]'));
        var currentId = 'tip-home-' + currentMatchId;
        var idx = all.findIndex(function (el) { return el.id === currentId; });

        // If we're starting from the away field of currentMatchId, the away input has already been replaced
        // by a read-only view, so we look for the next home input in DOM order.
        var startIdx = idx >= 0 ? idx + 1 : 0;
        for (var i = startIdx; i < all.length; i++) {
            // Only focus visible/enabled inputs
            if (!all[i].disabled && all[i].offsetParent !== null) {
                all[i].focus();
                all[i].select && all[i].select();
                return true;
            }
        }
        return false;
    }
};
