﻿using UnityEngine;
using Verse;

namespace FacialStuff
{
    // ReSharper disable UnassignedField.Global
    // ReSharper disable StyleCop.SA1307
    // ReSharper disable StyleCop.SA1401
    // ReSharper disable InconsistentNaming
    public class CompWeaponExtensions : ThingComp
    {
        #region Public Fields

        public CompProperties_WeaponExtensions Props
        {
            get { return (CompProperties_WeaponExtensions)this.props; }
        }

        #endregion Public Fields
    }
}