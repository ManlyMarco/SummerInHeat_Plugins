using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SiH_ModLoader
{
    public class Disposable : IDisposable
    {
        private Action _dispose;
        public Disposable(Action dispose)
        {
            if (dispose == null) throw new ArgumentNullException(nameof(dispose));
            _dispose = dispose;
        }
        public void Dispose()
        {
            if (_dispose != null)
            {
                _dispose();
                _dispose = null;
            }
        }

        public bool Disposed => _dispose == null;

        public static implicit operator Disposable(Action dispose)
        {
            return new Disposable(dispose);
        }

        public static implicit operator Disposable(Object obj)
        {
            return new Disposable(() =>
            {
                if (obj is Transform tr)
                {
                    if (!obj) return;

                    obj = tr.gameObject;
                }

                Object.Destroy(obj);
            });
        }
    }
}
