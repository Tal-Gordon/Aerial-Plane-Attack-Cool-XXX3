using UnityEngine;

public interface IEvolvableBrain : IBrain
{
    public void Copy(IEvolvableBrain brain);

    public void Mutate(float rate);

    public int[] GetShape();
}
