using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Common
{
    public class TokenEqualityCachedValue<TValue, TToken>
        where TValue : class
    {
        private CachedValue<TValue> _value;
        private TToken _token;

        public TokenEqualityCachedValue(Func<TValue> valueFunc, Func<TValue, TToken> tokenFunc, TToken initialTokenValue = default(TToken))
        {
            _value = new CachedValue<TValue>(valueFunc, (value) =>
            {
                var currentToken = _token;
                var newToken = tokenFunc(value);

                if (!newToken.HashCodeEquals(currentToken))
                {
                    _token = newToken;
                    return true;
                }

                return false;
            });

            _token = initialTokenValue;
        }

        public TValue Value => _value.Value;
        public TValue CurrentValue => _value.CurrentValue;

        public TToken CurrentToken => _token;

        public void RefreshIfInvalidated()
        {
            _value.RefreshIfInvalidated();
        }

        public static implicit operator TValue(TokenEqualityCachedValue<TValue, TToken> value)
        {
            return value.Value;
        }
    }
}