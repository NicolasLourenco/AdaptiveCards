/* ----------------------------------------------------------------------------
 * This file was automatically generated by SWIG (http://www.swig.org).
 * Version 4.0.2
 *
 * Do not make changes to this file unless you know what you are doing--modify
 * the SWIG interface file instead.
 * ----------------------------------------------------------------------------- */

package io.adaptivecards.objectmodel;

public class CaptionSourceVector extends java.util.AbstractList<CaptionSource> implements java.util.RandomAccess {
  private transient long swigCPtr;
  protected transient boolean swigCMemOwn;

  protected CaptionSourceVector(long cPtr, boolean cMemoryOwn) {
    swigCMemOwn = cMemoryOwn;
    swigCPtr = cPtr;
  }

  protected static long getCPtr(CaptionSourceVector obj) {
    return (obj == null) ? 0 : obj.swigCPtr;
  }

  @SuppressWarnings("deprecation")
  protected void finalize() {
    delete();
  }

  public synchronized void delete() {
    if (swigCPtr != 0) {
      if (swigCMemOwn) {
        swigCMemOwn = false;
        AdaptiveCardObjectModelJNI.delete_CaptionSourceVector(swigCPtr);
      }
      swigCPtr = 0;
    }
  }

  public CaptionSourceVector(CaptionSource[] initialElements) {
    this();
    reserve(initialElements.length);

    for (CaptionSource element : initialElements) {
      add(element);
    }
  }

  public CaptionSourceVector(Iterable<CaptionSource> initialElements) {
    this();
    for (CaptionSource element : initialElements) {
      add(element);
    }
  }

  public CaptionSource get(int index) {
    return doGet(index);
  }

  public CaptionSource set(int index, CaptionSource e) {
    return doSet(index, e);
  }

  public boolean add(CaptionSource e) {
    modCount++;
    doAdd(e);
    return true;
  }

  public void add(int index, CaptionSource e) {
    modCount++;
    doAdd(index, e);
  }

  public CaptionSource remove(int index) {
    modCount++;
    return doRemove(index);
  }

  protected void removeRange(int fromIndex, int toIndex) {
    modCount++;
    doRemoveRange(fromIndex, toIndex);
  }

  public int size() {
    return doSize();
  }

  public CaptionSourceVector() {
    this(AdaptiveCardObjectModelJNI.new_CaptionSourceVector__SWIG_0(), true);
  }

  public CaptionSourceVector(CaptionSourceVector other) {
    this(AdaptiveCardObjectModelJNI.new_CaptionSourceVector__SWIG_1(CaptionSourceVector.getCPtr(other), other), true);
  }

  public long capacity() {
    return AdaptiveCardObjectModelJNI.CaptionSourceVector_capacity(swigCPtr, this);
  }

  public void reserve(long n) {
    AdaptiveCardObjectModelJNI.CaptionSourceVector_reserve(swigCPtr, this, n);
  }

  public boolean isEmpty() {
    return AdaptiveCardObjectModelJNI.CaptionSourceVector_isEmpty(swigCPtr, this);
  }

  public void clear() {
    AdaptiveCardObjectModelJNI.CaptionSourceVector_clear(swigCPtr, this);
  }

  public CaptionSourceVector(int count, CaptionSource value) {
    this(AdaptiveCardObjectModelJNI.new_CaptionSourceVector__SWIG_2(count, CaptionSource.getCPtr(value), value), true);
  }

  private int doSize() {
    return AdaptiveCardObjectModelJNI.CaptionSourceVector_doSize(swigCPtr, this);
  }

  private void doAdd(CaptionSource x) {
    AdaptiveCardObjectModelJNI.CaptionSourceVector_doAdd__SWIG_0(swigCPtr, this, CaptionSource.getCPtr(x), x);
  }

  private void doAdd(int index, CaptionSource x) {
    AdaptiveCardObjectModelJNI.CaptionSourceVector_doAdd__SWIG_1(swigCPtr, this, index, CaptionSource.getCPtr(x), x);
  }

  private CaptionSource doRemove(int index) {
    long cPtr = AdaptiveCardObjectModelJNI.CaptionSourceVector_doRemove(swigCPtr, this, index);
    return (cPtr == 0) ? null : new CaptionSource(cPtr, true);
  }

  private CaptionSource doGet(int index) {
    long cPtr = AdaptiveCardObjectModelJNI.CaptionSourceVector_doGet(swigCPtr, this, index);
    return (cPtr == 0) ? null : new CaptionSource(cPtr, true);
  }

  private CaptionSource doSet(int index, CaptionSource val) {
    long cPtr = AdaptiveCardObjectModelJNI.CaptionSourceVector_doSet(swigCPtr, this, index, CaptionSource.getCPtr(val), val);
    return (cPtr == 0) ? null : new CaptionSource(cPtr, true);
  }

  private void doRemoveRange(int fromIndex, int toIndex) {
    AdaptiveCardObjectModelJNI.CaptionSourceVector_doRemoveRange(swigCPtr, this, fromIndex, toIndex);
  }

}
