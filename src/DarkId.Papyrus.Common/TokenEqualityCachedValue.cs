using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DarkId.Papyrus.Common
{
    public class TokenEqualityCachedValue<TValue, TToken>
        where TValue : class
    {
        private readonly CachedValue<TValue> _value;

        public TokenEqualityCachedValue(Func<TValue> valueFunc, Func<TValue, TToken> tokenFunc, TToken initialTokenValue = default)
        {
            _value = new CachedValue<TValue>(valueFunc, (value) =>
            {
                var currentToken = CurrentToken;
                var newToken = tokenFunc(value);

                if (!newToken.HashCodeEquals(currentToken))
                {
                    CurrentToken = newToken;
                    return true;
                }

                return false;
            });

            CurrentToken = initialTokenValue;
        }

        public TValue Value => _value.Value;
        public TValue CurrentValue => _value.CurrentValue;

        public TToken CurrentToken { get; private set; }

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