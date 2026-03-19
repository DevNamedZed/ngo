package main

/*
#include <stdio.h>
#include <stdlib.h>
#include <math.h>

int add(int a, int b) {
    return a + b;
}

double my_sqrt(double x) {
    return sqrt(x);
}

void hello() {
    printf("Hello from C!\n");
}
*/
import "C"
import "fmt"

func main() {
	// Call C functions
	sum := C.add(C.int(3), C.int(4))
	fmt.Println("3 + 4 =", sum)

	root := C.my_sqrt(C.double(2.0))
	fmt.Println("sqrt(2) =", root)

	C.hello()

	fmt.Println("CGo test passed!")
}
