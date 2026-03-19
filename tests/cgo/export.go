package main

/*
#include <stdio.h>

// Forward declaration — implemented in Go via //export
extern int goAdd(int a, int b);

void call_go_add() {
    int result = goAdd(10, 20);
    printf("Go returned: %d\n", result);
}
*/
import "C"
import "fmt"

//export goAdd
func goAdd(a, b C.int) C.int {
	return a + b
}

func main() {
	// Call C function that calls back into Go
	C.call_go_add()
	fmt.Println("CGo export test passed!")
}
