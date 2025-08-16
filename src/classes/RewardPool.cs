using System.Collections.Generic;

namespace Huntdown
{
    public enum RewardPoolKey
    {
        EasyRewardPool,
        MediumRewardPool,
        HardRewardPool,
        ExtremeRewardPool,
        MaskRewardPool,
        BrutalRewardPool,
        ExecutionRewardPool
    }

    public class RewardPool
    {
        private List<StoredItem> _rewards;
        private int _value;

        public RewardPool(StoredItem[] rewards, int value)
        {
            _rewards = new List<StoredItem>(rewards);
            _value = value;
        }

        public List<StoredItem> Rewards
        {
            get { return _rewards; }
            set { _rewards = value; }
        }

        public int Value
        {
            get { return _value; }
            set { _value = value; }
        }
    }
}