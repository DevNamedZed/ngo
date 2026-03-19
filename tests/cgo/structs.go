package main

/*
typedef struct {
    int x;
    int y;
} Point;

typedef struct {
    Point origin;
    int width;
    int height;
} Rect;

Point make_point(int x, int y) {
    Point p;
    p.x = x;
    p.y = y;
    return p;
}

int point_sum(Point p) {
    return p.x + p.y;
}

int rect_area(Rect r) {
    return r.width * r.height;
}
*/
import "C"
import "fmt"

func main() {
	p := C.make_point(C.int(10), C.int(20))
	fmt.Println("Point:", p.x, p.y)

	sum := C.point_sum(p)
	fmt.Println("Sum:", sum)

	fmt.Println("CGo struct test passed!")
}
