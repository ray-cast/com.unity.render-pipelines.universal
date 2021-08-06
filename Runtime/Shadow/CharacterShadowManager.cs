using System;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace UnityEngine.Rendering.Universal
{
    public sealed class CharacterShadowManager
    {
        static readonly Lazy<CharacterShadowManager> s_Instance = new Lazy<CharacterShadowManager>(() => new CharacterShadowManager());

        public static CharacterShadowManager instance => s_Instance.Value;

        internal readonly List<CharacterShadow> _perObjectShadows;

        CharacterShadowManager()
        {
            _perObjectShadows = new List<CharacterShadow>();
        }

        public void Register(CharacterShadow occluder, int layer)
        {
            _perObjectShadows.Add(occluder);
        }

        public void Unregister(CharacterShadow occluder, int layer)
        {
            _perObjectShadows.Remove(occluder);
        }
    }
}