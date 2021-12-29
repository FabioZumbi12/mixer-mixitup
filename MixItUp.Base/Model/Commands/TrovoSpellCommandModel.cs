﻿using System.Runtime.Serialization;

namespace MixItUp.Base.Model.Commands
{
    [DataContract]
    public class TrovoSpellCommandModel : CommandModelBase
    {
        public TrovoSpellCommandModel(string name)
            : base(name, CommandTypeEnum.TrovoSpell)
        { }

        protected TrovoSpellCommandModel() : base() { }
    }
}
