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
