namespace QAQ
{
    public class Solution
    {
        public int HIndex_A(int[] citations)
        {
            if (citations == null || citations.Length == 0) return 0;
            Array.Sort(citations);
            Array.Reverse(citations);
            int h = 0;
            for(int i=0;i<citations.Length;++i)
            {
                if (citations[i]>=i+1)
                {
                    h = i + 1;
                }
                else
                {
                    break;
                }
            }
            return h;
        }

        public int HIndex_B(int[] citations)
        {
            int[] nums = new int[citations.Length + 1];
            for (int i = 0; i < citations.Length; ++i)
            {
                nums[Math.Min(citations.Length, citations[i])]++;
            }
            int sum = 0;
            for (int i = citations.Length; i >= 0; --i)
            {
                sum += nums[i];
                if (sum >= i)
                {
                    return i;
                }
            }
            return 0;
        }
    }
}
/*
输入：citations = [3,0,6,1,5]
输出：3 
解释：给定数组表示研究者总共有 5 篇论文，每篇论文相应的被引用了 3, 0, 6, 1, 5 次。
     由于研究者有 3 篇论文每篇 至少 被引用了 3 次，其余两篇论文每篇被引用 不多于 3 次，所以她的 h 指数是 3。输入：citations = [3,0,6,1,5]
输出：3 
解释：给定数组表示研究者总共有 5 篇论文，每篇论文相应的被引用了 3, 0, 6, 1, 5 次。
     由于研究者有 3 篇论文每篇 至少 被引用了 3 次，其余两篇论文每篇被引用 不多于 3 次，所以她的 h 指数是 3
*/